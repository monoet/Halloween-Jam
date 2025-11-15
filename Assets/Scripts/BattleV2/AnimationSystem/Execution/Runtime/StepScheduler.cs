using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using BattleV2.AnimationSystem;
using BattleV2.AnimationSystem.Execution.Runtime.Core;
using BattleV2.AnimationSystem.Execution.Runtime.Core.Conflict;
using BattleV2.AnimationSystem.Execution.Runtime.Core.GroupRunners;
using BattleV2.AnimationSystem.Execution.Runtime.SystemSteps;
using BattleV2.Core;

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

            try
            {
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
                NotifyObservers(o => o.OnRecipeCompleted(new RecipeExecutionReport(recipe, recipeWatch.Elapsed, recipeCancelled || state.AbortRequested), context));
            }
            finally
            {
                if (!context.SkipResetToFallback)
                {
                    context.Wrapper?.ResetToFallback();
                }
            }
        }

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
                    skipResetToFallback: true).ConfigureAwait(false);
            }

            fallbackReset |= await ExecuteLifecyclePhaseAsync(
                ActionLifecyclePhase.Action,
                context,
                cancellationToken,
                inlineRecipe: recipe,
                recipeId: recipe?.Id,
                skipResetToFallback: true).ConfigureAwait(false);

            if (!ShouldSkip(postId))
            {
                fallbackReset |= await ExecuteLifecyclePhaseAsync(
                    ActionLifecyclePhase.PostAction,
                    context,
                    cancellationToken,
                    inlineRecipe: null,
                    recipeId: postId,
                    skipResetToFallback: false).ConfigureAwait(false);
            }

            if (!fallbackReset && !context.SkipResetToFallback)
            {
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

            bool resetTriggered = false;
            try
            {
                if (phaseRecipe != null && !phaseRecipe.IsEmpty)
                {
                    var finalSkipReset = skipResetToFallback || context.SkipResetToFallback;
                    var phaseContext = context.WithSkipReset(finalSkipReset);
                    await ExecuteAsync(phaseRecipe, phaseContext, cancellationToken).ConfigureAwait(false);
                    resetTriggered = !finalSkipReset;
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
            }

            return resetTriggered;
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

            IStepSchedulerObserver[] snapshot;
            lock (executionGate)
            {
                if (observers.Count == 0)
                {
                    return;
                }

                snapshot = observers.ToArray();
            }

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

    }

}


