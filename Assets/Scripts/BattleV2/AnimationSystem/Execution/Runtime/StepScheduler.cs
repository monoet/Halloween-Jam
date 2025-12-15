using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BattleV2.AnimationSystem;
using BattleV2.AnimationSystem.Execution.Runtime.Core;
using BattleV2.AnimationSystem.Execution.Runtime.Core.Conflict;
using BattleV2.AnimationSystem.Execution.Runtime.Core.GroupRunners;
using BattleV2.AnimationSystem.Execution.Runtime.SystemSteps;
using BattleV2.Core;
using BattleV2.Diagnostics;
using UnityEngine;

namespace BattleV2.AnimationSystem.Execution.Runtime
{
    /// <summary>
    /// Coordinates execution of action recipes by delegating steps to registered executors.
    /// </summary>
    public sealed class StepScheduler
    {
        private const string LogTag = "StepScheduler";

        private readonly Dictionary<string, IActionStepExecutor> executors = new Dictionary<string, IActionStepExecutor>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ActionRecipe> recipeRegistry = new Dictionary<string, ActionRecipe>(StringComparer.OrdinalIgnoreCase);
        private readonly List<IStepSchedulerObserver> observers = new List<IStepSchedulerObserver>();
        private readonly object executionGate = new object();
        private IMainThreadInvoker observerInvoker;
        private readonly SystemStepRunner systemStepRunner;
        private readonly SequentialGroupRunner sequentialRunner;
        private readonly ParallelGroupRunner parallelRunner;
        private readonly ActiveExecutionRegistry activeExecutionRegistry;
        private ActionLifecycleConfig lifecycleConfig = ActionLifecycleConfig.Default;

        public event Action<ActionLifecycleEventArgs> LifecycleEvent;

        public StepScheduler()
        {
            systemStepRunner = new SystemStepRunner(LogTag);
            sequentialRunner = new SequentialGroupRunner(this);
            parallelRunner = new ParallelGroupRunner(this);
            activeExecutionRegistry = new ActiveExecutionRegistry(LogTag);
        }

        public ActionLifecycleConfig LifecycleConfig => lifecycleConfig;

        public void ConfigureObserverInvoker(IMainThreadInvoker invoker)
        {
            observerInvoker = invoker;
        }

        public void ConfigureLifecycle(ActionLifecycleConfig config)
        {
            lifecycleConfig = config ?? ActionLifecycleConfig.Default;
        }

        public void RegisterExecutor(IActionStepExecutor executor)
        {
            if (executor == null)
            {
                throw new ArgumentNullException(nameof(executor));
            }

            lock (executionGate)
            {
                executors[executor.Id] = executor;
            }
        }

        public bool UnregisterExecutor(string executorId)
        {
            if (string.IsNullOrWhiteSpace(executorId))
            {
                return false;
            }

            lock (executionGate)
            {
                return executors.Remove(executorId);
            }
        }

        public void ClearExecutors()
        {
            lock (executionGate)
            {
                executors.Clear();
            }
        }

        public void RegisterRecipe(ActionRecipe recipe)
        {
            if (recipe == null || string.IsNullOrWhiteSpace(recipe.Id))
            {
                return;
            }

            lock (executionGate)
            {
                recipeRegistry[recipe.Id] = recipe;
            }
        }

        public bool TryGetRecipe(string recipeId, out ActionRecipe recipe)
        {
            if (string.IsNullOrWhiteSpace(recipeId))
            {
                recipe = null;
                return false;
            }

            lock (executionGate)
            {
                return recipeRegistry.TryGetValue(recipeId, out recipe);
            }
        }

        public bool UnregisterRecipe(string recipeId)
        {
            if (string.IsNullOrWhiteSpace(recipeId))
            {
                return false;
            }

            lock (executionGate)
            {
                return recipeRegistry.Remove(recipeId);
            }
        }

        public void RegisterObserver(IStepSchedulerObserver observer)
        {
            if (observer == null)
            {
                return;
            }

            lock (executionGate)
            {
                if (!observers.Contains(observer))
                {
                    observers.Add(observer);
                }
            }
        }

        public void UnregisterObserver(IStepSchedulerObserver observer)
        {
            if (observer == null)
            {
                return;
            }

            lock (executionGate)
            {
                observers.Remove(observer);
            }
        }

        public async Task ExecuteAsync(ActionRecipe recipe, StepSchedulerContext context, CancellationToken cancellationToken = default)
        {
            if (recipe == null || recipe.IsEmpty)
            {
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ValidateRecipeDevOnly(recipe, context);
#endif

            var gate = context.Gate;
            var skipByMotion = ShouldSkipReset(recipe);
            try
            {
                LogPoseSnapshot(context.Actor, recipe.Id ?? "(null)", true);
                NotifyObservers(o => o.OnRecipeStarted(recipe, context));

                using var state = new ExecutionState(recipe, context, LogTag);
                state.Initialize();

                var recipeWatch = Stopwatch.StartNew();
                bool recipeCancelled = false;
                int groupIndex = 0;

                while (groupIndex < recipe.Groups.Count)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var group = recipe.Groups[groupIndex];
                    gate?.BeginGroup(group.Id);
                    NotifyObservers(o => o.OnGroupStarted(group, context));
                    var groupWatch = Stopwatch.StartNew();
                    StepGroupResult groupResult = StepGroupResult.Completed();

                    try
                    {
                        groupResult = await ExecuteGroupAsync(group, context, state, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        recipeCancelled = true;
                        groupResult = StepGroupResult.Abort("Cancelled");
                        throw;
                    }
                    finally
                    {
                        groupWatch.Stop();
                        NotifyObservers(o => o.OnGroupCompleted(new StepGroupExecutionReport(group, groupWatch.Elapsed, groupResult.Status == StepGroupResultStatus.Abort), context));
                    }

                    if (gate != null)
                    {
                        await gate.AwaitGroupAsync(cancellationToken);
                    }

                    if (groupResult.Status == StepGroupResultStatus.Branch)
                    {
                        if (!state.TryGetGroupIndex(groupResult.BranchTargetId, out var target))
                        {
                            BattleLogger.Warn(LogTag, $"Branch target '{groupResult.BranchTargetId ?? "(null)"}' not found. Ending recipe '{recipe.Id}'.");
                            break;
                        }

                        groupIndex = target;
                        continue;
                    }

                    if (groupResult.Status == StepGroupResultStatus.Abort)
                    {
                        recipeCancelled = true;
                        if (!string.IsNullOrWhiteSpace(groupResult.AbortReason))
                        {
                            state.RequestAbort(groupResult.AbortReason);
                        }

                        state.ImmediateCleanup();
                        break;
                    }

                    groupIndex++;
                }

                recipeWatch.Stop();
                if (gate != null)
                {
                    await gate.AwaitAllAsync(cancellationToken);
                }
                NotifyObservers(o => o.OnRecipeCompleted(new RecipeExecutionReport(recipe, recipeWatch.Elapsed, recipeCancelled || state.AbortRequested), context));
                LogPoseSnapshot(context.Actor, recipe.Id ?? "(null)", false);
            }
            finally
            {
                if (!(context.SkipResetToFallback || skipByMotion))
                {
                    context.Wrapper?.ResetToFallback();
                }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                else
                {
                    UnityEngine.Debug.Log($"TTDebug11 [POSE/FALLBACK_SKIPPED] actor={context.Actor?.name ?? "(null)"} recipe={recipe?.Id ?? "(null)"} reason={(skipByMotion ? "motionRecipe" : "contextSkip")}");
                }
#endif
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static readonly HashSet<string> LocomotionGroupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "run_up",
            "run_up_target",
            "run_back",
            "move_to_target",
            "move_to_spotlight",
            "return_home"
        };

        private static readonly HashSet<string> ForbiddenPayloadExecutors = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "reset.fallback"
        };

        private static string BuildRecipeDump(ActionRecipe recipe, int maxGroups = 12, int maxExecutors = 4)
        {
            if (recipe == null || recipe.IsEmpty)
            {
                return "Plan: <empty>";
            }

            var groups = recipe.Groups;
            if (groups == null || groups.Count == 0)
            {
                return $"Plan: <no groups> recipeId={recipe.Id ?? "(null)"}";
            }

            var sb = new StringBuilder(capacity: 256);
            sb.AppendLine($"Plan recipeId={recipe.Id ?? "(null)"} groups={groups.Count}");

            var count = Math.Min(groups.Count, maxGroups);
            for (int i = 0; i < count; i++)
            {
                var group = groups[i];
                if (group == null)
                {
                    sb.AppendLine($"- [{i}] <null group>");
                    continue;
                }

                sb.Append($"- [{i}] groupId={group.Id ?? "(null)"} kind={group.Kind ?? "(null)"} mode={group.ExecutionMode} join={group.JoinPolicy}");

                if (group.Steps == null || group.Steps.Count == 0)
                {
                    sb.AppendLine(" steps=[]");
                    continue;
                }

                sb.Append(" exec=[");
                var stepsToPrint = Math.Min(group.Steps.Count, maxExecutors);
                for (int j = 0; j < stepsToPrint; j++)
                {
                    if (j > 0)
                    {
                        sb.Append(",");
                    }

                    sb.Append(group.Steps[j].ExecutorId ?? "(null)");
                }

                if (group.Steps.Count > stepsToPrint)
                {
                    sb.Append($",...(+{group.Steps.Count - stepsToPrint})");
                }

                sb.AppendLine("]");
            }

            if (groups.Count > count)
            {
                sb.AppendLine($"...(+{groups.Count - count} groups)");
            }

            return sb.ToString();
        }

        private static void ValidateRecipeDevOnly(ActionRecipe recipe, StepSchedulerContext context)
        {
            if (recipe == null || recipe.IsEmpty)
            {
                return;
            }

            bool hasLegacyBridgeStep = false;
            for (int i = 0; i < recipe.Groups.Count; i++)
            {
                var group = recipe.Groups[i];
                if (group == null || group.Steps == null)
                {
                    continue;
                }

                for (int j = 0; j < group.Steps.Count; j++)
                {
                    var step = group.Steps[j];
                    if (string.Equals(step.ExecutorId, BattleV2.AnimationSystem.Execution.Runtime.Executors.LegacyPlaybackExecutor.ExecutorId, StringComparison.OrdinalIgnoreCase))
                    {
                        hasLegacyBridgeStep = true;
                        break;
                    }
                }

                if (hasLegacyBridgeStep)
                {
                    break;
                }
            }

            if (hasLegacyBridgeStep && context.ResetPolicy != ResetPolicy.DeferUntilPlanFinally)
            {
                var dump = BattleDebug.IsEnabled("SS") ? "\n" + BuildRecipeDump(recipe) : string.Empty;
                BattleDebug.Warn("SS", 902, $"INVALID legacy bridge requires ResetPolicy=DeferUntilPlanFinally recipeId={recipe.Id}{dump}", context.Actor);
            }

            for (int i = 0; i < recipe.Groups.Count; i++)
            {
                var group = recipe.Groups[i];
                if (group == null)
                {
                    continue;
                }

                if (!string.Equals(group.Kind, "payload", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (group.JoinPolicy == StepGroupJoinPolicy.Any)
                {
                    var dump = BattleDebug.IsEnabled("SS") ? "\n" + BuildRecipeDump(recipe) : string.Empty;
                    BattleDebug.Warn("SS", 900, $"INVALID payload joinPolicy=Any recipeId={recipe.Id} groupId={group.Id ?? "(null)"}{dump}", context.Actor);
                }

                if (!string.IsNullOrWhiteSpace(group.Id) && LocomotionGroupIds.Contains(group.Id))
                {
                    var dump = BattleDebug.IsEnabled("SS") ? "\n" + BuildRecipeDump(recipe) : string.Empty;
                    BattleDebug.Warn("SS", 901, $"INVALID payload touches locomotion recipeId={recipe.Id} groupId={group.Id}{dump}", context.Actor);
                }

                if (group.Steps != null)
                {
                    for (int j = 0; j < group.Steps.Count; j++)
                    {
                        var step = group.Steps[j];
                        if (ForbiddenPayloadExecutors.Contains(step.ExecutorId))
                        {
                            var dump = BattleDebug.IsEnabled("SS") ? "\n" + BuildRecipeDump(recipe) : string.Empty;
                            BattleDebug.Warn("SS", 901, $"INVALID payload touches locomotion recipeId={recipe.Id} groupId={group.Id ?? "(null)"} executorId={step.ExecutorId}{dump}", context.Actor);
                        }
                    }
                }
            }
        }
#endif

        public async Task ExecuteLifecycleAsync(ActionRecipe recipe, StepSchedulerContext context, CancellationToken cancellationToken = default)
        {
            var config = lifecycleConfig ?? ActionLifecycleConfig.Default;
            bool fallbackReset = false;
            var actionId = recipe?.Id;
            var preId = config?.RunUpRecipeId;
            var postId = config?.RunBackRecipeId;

            bool ShouldSkip(string lifecycleId) =>
                string.IsNullOrWhiteSpace(lifecycleId) ||
                (actionId != null && string.Equals(actionId, lifecycleId, StringComparison.OrdinalIgnoreCase));

            if (!ShouldSkip(preId))
            {
                fallbackReset |= await ExecuteLifecyclePhaseAsync(
                    ActionLifecyclePhase.PreAction,
                    context,
                    cancellationToken,
                    inlineRecipe: null,
                    recipeId: preId,
                    skipResetToFallback: true);
            }

            fallbackReset |= await ExecuteLifecyclePhaseAsync(
                ActionLifecyclePhase.Action,
                context,
                cancellationToken,
                inlineRecipe: recipe,
                recipeId: recipe?.Id,
                skipResetToFallback: true);

            if (!ShouldSkip(postId))
            {
                fallbackReset |= await ExecuteLifecyclePhaseAsync(
                    ActionLifecyclePhase.PostAction,
                    context,
                    cancellationToken,
                    inlineRecipe: null,
                    recipeId: postId,
                    skipResetToFallback: false);
            }

            if (!fallbackReset && !context.SkipResetToFallback)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                UnityEngine.Debug.Log($"TTDebug14 [POSE/FALLBACK_RESET] actor={context.Actor?.name ?? "(null)"} recipe={recipe?.Id ?? "(null)"} reason=lifecycle-end");
#endif
                context.Wrapper?.ResetToFallback();
            }
        }

        private async Task<bool> ExecuteLifecyclePhaseAsync(
            ActionLifecyclePhase phase,
            StepSchedulerContext context,
            CancellationToken cancellationToken,
            ActionRecipe inlineRecipe,
            string recipeId,
            bool skipResetToFallback)
        {
            var phaseRecipe = inlineRecipe;
            if (phaseRecipe == null && !string.IsNullOrWhiteSpace(recipeId))
            {
                TryGetRecipe(recipeId, out phaseRecipe);
            }

            var beginArgs = new ActionLifecycleEventArgs(
                ActionLifecycleEvents.GetEventId(phase, ActionLifecycleEventType.Begin),
                phase,
                ActionLifecycleEventType.Begin,
                phaseRecipe,
                context);
            NotifyLifecycleListeners(beginArgs);
            LogPoseSnapshot(context.Actor, phase.ToString(), true);

            bool resetTriggered = false;
            bool skippedReset = false;
            try
            {
                if (phaseRecipe != null && !phaseRecipe.IsEmpty)
                {
                    var finalSkipReset = skipResetToFallback || context.SkipResetToFallback || ShouldSkipReset(phaseRecipe);
                    var phaseContext = context.WithSkipReset(finalSkipReset);
                    await ExecuteAsync(phaseRecipe, phaseContext, cancellationToken);
                    resetTriggered = !finalSkipReset;
                    skippedReset = finalSkipReset;
                }
            }
            finally
            {
                var endArgs = new ActionLifecycleEventArgs(
                    ActionLifecycleEvents.GetEventId(phase, ActionLifecycleEventType.End),
                    phase,
                    ActionLifecycleEventType.End,
                    phaseRecipe,
                    context);
                NotifyLifecycleListeners(endArgs);
                LogPoseSnapshot(context.Actor, phase.ToString(), false);
            }

            return resetTriggered || skippedReset;
        }

        private Task<StepGroupResult> ExecuteGroupAsync(
            ActionStepGroup group,
            StepSchedulerContext context,
            ExecutionState state,
            CancellationToken cancellationToken)
        {
            return group.ExecutionMode == StepGroupExecutionMode.Sequential
                ? sequentialRunner.ExecuteAsync(group, context, state, cancellationToken)
                : parallelRunner.ExecuteAsync(group, context, state, cancellationToken);
        }

        internal async Task<StepResult> ExecuteStepInternalAsync(
            ActionStep step,
            StepSchedulerContext schedulerContext,
            ExecutionState state,
            CancellationToken cancellationToken,
            bool swallowCancellation)
        {
            NotifyObservers(o => o.OnStepStarted(step, schedulerContext));
            var watch = Stopwatch.StartNew();
            StepResult result = StepResult.Completed;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (systemStepRunner.TryHandle(step, schedulerContext, state, out var systemResult))
                {
                    result = systemResult;
                }
                else if (!TryResolveExecutor(step, out var executor))
                {
                    result = StepResult.Skipped;
                }
                else
                {
                    result = await ExecuteExecutorStepAsync(executor, step, schedulerContext, cancellationToken, swallowCancellation);
                }
            }
            catch (OperationCanceledException)
            {
                if (!swallowCancellation)
                {
                    throw;
                }

                result = StepResult.Abort("Cancelled");
            }
            catch (Exception ex)
            {
                BattleLogger.Error(LogTag, $"Step '{step.Id ?? "(no id)"}' crashed: {ex}");
                result = StepResult.Failed;
            }
            finally
            {
                watch.Stop();
                NotifyObservers(o => o.OnStepCompleted(new StepExecutionReport(step, MapOutcome(result.Status), watch.Elapsed), schedulerContext));
            }

            if (result.Status == StepRunStatus.Branch)
            {
                var sourceId = step.Id ?? step.ExecutorId ?? "(no id)";
                var targetId = result.BranchTargetId ?? string.Empty;
                NotifyObservers(o => o.OnBranchTaken(sourceId, targetId, schedulerContext));
                BattleLogger.Log(LogTag, $"Step '{step.Id ?? "(no id)"}' branched to '{result.BranchTargetId ?? "(null)"}'.");
            }
            else if (result.Status == StepRunStatus.Abort)
            {
                BattleLogger.Warn(LogTag, $"Step '{step.Id ?? "(no id)"}' aborted. Reason={result.AbortReason ?? "(null)"}.");
            }

            return result;
        }

        private async Task<StepResult> ExecuteExecutorStepAsync(
            IActionStepExecutor executor,
            ActionStep step,
            StepSchedulerContext schedulerContext,
            CancellationToken cancellationToken,
            bool swallowCancellation)
        {
            var effectivePolicy = GetEffectiveConflictPolicy(step, executor);

            if (!await activeExecutionRegistry.ResolveConflictAsync(executor.Id, effectivePolicy, cancellationToken))
            {
                BattleLogger.Log(LogTag, $"Step '{step.Id ?? "(no id)"}' skipped due to conflict policy '{effectivePolicy}'.");
                return StepResult.Skipped;
            }

            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var context = new StepExecutionContext(schedulerContext, step, linkedCts.Token);
            var executionTask = RunExecutorAsync(executor, context, linkedCts);
            activeExecutionRegistry.Register(executor.Id, executionTask, linkedCts);

            try
            {
                await executionTask;
                return StepResult.Completed;
            }
            catch (OperationCanceledException)
            {
                if (!swallowCancellation)
                {
                    throw;
                }

                return StepResult.Abort("Cancelled");
            }
            catch (Exception ex)
            {
                BattleLogger.Error(LogTag, $"Executor '{executor.Id}' crashed while running step '{step.Id ?? "(no id)"}': {ex}");
                return StepResult.Failed;
            }
            finally
            {
                activeExecutionRegistry.Remove(executor.Id, executionTask);
            }
        }

        private StepConflictPolicy GetEffectiveConflictPolicy(ActionStep step, IActionStepExecutor executor)
        {
            if (step.HasExplicitConflictPolicy || executor == null)
            {
                return step.ConflictPolicy;
            }

            string executorId = executor.Id ?? string.Empty;
            if (string.Equals(executorId, "animatorclip", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(executorId, "tween", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(executorId, "flipbook", StringComparison.OrdinalIgnoreCase))
            {
                return StepConflictPolicy.WaitForCompletion;
            }

            if (string.Equals(executorId, "sfx", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(executorId, "vfx", StringComparison.OrdinalIgnoreCase))
            {
                return StepConflictPolicy.SkipIfRunning;
            }

            return step.ConflictPolicy;
        }

        private static StepExecutionOutcome MapOutcome(StepRunStatus status)
        {
            return status switch
            {
                StepRunStatus.Completed => StepExecutionOutcome.Completed,
                StepRunStatus.Branch => StepExecutionOutcome.Branch,
                StepRunStatus.Skipped => StepExecutionOutcome.Skipped,
                StepRunStatus.Abort => StepExecutionOutcome.Cancelled,
                StepRunStatus.Failed => StepExecutionOutcome.Faulted,
                _ => StepExecutionOutcome.Completed
            };
        }

        private bool TryResolveExecutor(in ActionStep step, out IActionStepExecutor executor)
        {
            lock (executionGate)
            {
                if (!executors.TryGetValue(step.ExecutorId, out executor))
                {
                    BattleLogger.Warn(LogTag, $"Executor '{step.ExecutorId}' not registered. Step '{step.Id ?? "(no id)"}' skipped.");
                    return false;
                }
            }

            if (!executor.CanExecute(step))
            {
                BattleLogger.Warn(LogTag, $"Executor '{executor.Id}' declined step '{step.Id ?? "(no id)"}'.");
                executor = null;
                return false;
            }

            return true;
        }

        private static async Task RunExecutorAsync(IActionStepExecutor executor, StepExecutionContext context, CancellationTokenSource linkedCts)
        {
            try
            {
                if (context.Step.HasDelay)
                {
                    var delay = TimeSpan.FromSeconds(context.Step.DelaySeconds);
                    await Task.Delay(delay, context.CancellationToken);
                }

                await executor.ExecuteAsync(context);
            }
            finally
            {
                linkedCts.Dispose();
            }
        }

        private void NotifyObservers(Action<IStepSchedulerObserver> notify)
        {
            if (notify == null)
            {
                return;
            }

            if (BattleDebug.IsEnabled("SS"))
            {
                BattleDebug.Log(
                    "SS",
                    1,
                    $"NotifyObservers threadId={Thread.CurrentThread.ManagedThreadId} isMain={BattleDebug.IsMainThread}",
                    context: null);
                if (!BattleDebug.IsMainThread)
                {
                    BattleDebug.Warn("SS", 2, "Observer notification running off-main-thread (risk: Unity/DOTween non-determinism).");
                }
            }

            IStepSchedulerObserver[] snapshot;
            lock (executionGate)
            {
                if (observers.Count == 0)
                {
                    return;
                }

                snapshot = observers.ToArray();
            }

            if (observerInvoker != null && !BattleDebug.IsMainThread)
            {
                observerInvoker.RunAsync(() =>
                {
                    NotifyObserverSnapshot(snapshot, notify);
                    return Task.CompletedTask;
                }).GetAwaiter().GetResult();
                return;
            }

            NotifyObserverSnapshot(snapshot, notify);
        }

        private static void NotifyObserverSnapshot(IStepSchedulerObserver[] snapshot, Action<IStepSchedulerObserver> notify)
        {
            for (int i = 0; i < snapshot.Length; i++)
            {
                try
                {
                    notify(snapshot[i]);
                }
                catch (Exception ex)
                {
                    BattleLogger.Warn(LogTag, $"Observer '{snapshot[i].GetType().Name}' threw during notification: {ex.Message}");
                }
            }
        }

        private void NotifyLifecycleListeners(ActionLifecycleEventArgs args)
        {
            var handler = LifecycleEvent;
            if (handler == null)
            {
                return;
            }

            var listeners = handler.GetInvocationList();
            for (int i = 0; i < listeners.Length; i++)
            {
                var listener = (Action<ActionLifecycleEventArgs>)listeners[i];
                try
                {
                    listener(args);
                }
                catch (Exception ex)
                {
                    var listenerName = listener.Target?.GetType().Name ?? "(unknown)";
                    BattleLogger.Warn(LogTag, $"Lifecycle observer '{listenerName}' threw: {ex.Message}");
                }
            }
        }

        private static bool ShouldSkipReset(ActionRecipe recipe)
        {
            if (recipe == null)
            {
                return false;
            }

            var id = recipe.Id;
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            return string.Equals(id, "run_up", StringComparison.OrdinalIgnoreCase)
                || string.Equals(id, "run_back", StringComparison.OrdinalIgnoreCase);
        }

        private static void LogPoseSnapshot(CombatantState actor, string phaseLabel, bool isPre)
        {
            if (actor == null)
            {
                return;
            }

            var t = actor.transform;
            if (t == null)
            {
                return;
            }

            var timestamp = TimeSpan.FromSeconds(Time.realtimeSinceStartup).ToString(@"mm\:ss\.fff");
            var tag = isPre ? "TTDebug07" : "TTDebug08";
            UnityEngine.Debug.Log($"{tag} [POSE] time={timestamp} actor={actor.name} local={t.localPosition} world={t.position} parent={t.parent?.name ?? "(null)"} phase={phaseLabel}");
        }

    }

}
