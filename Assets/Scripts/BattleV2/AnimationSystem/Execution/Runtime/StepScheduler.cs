using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using BattleV2.AnimationSystem;
using BattleV2.Core;

namespace BattleV2.AnimationSystem.Execution.Runtime
{
    /// <summary>
    /// Coordinates execution of action recipes by delegating steps to registered executors.
    /// </summary>
    public sealed class StepScheduler
    {
        private const string LogTag = "StepScheduler";

        private const string SystemStepWindowOpen = "window.open";
        private const string SystemStepWindowClose = "window.close";
        private const string SystemStepGate = "gate.on";
        private const string SystemStepDamage = "damage.apply";
        private const string SystemStepFallback = "fallback";

        private static readonly TimedHitJudgment[] DefaultSuccessJudgments =
        {
            TimedHitJudgment.Perfect,
            TimedHitJudgment.Good
        };

        private readonly Dictionary<string, IActionStepExecutor> executors = new Dictionary<string, IActionStepExecutor>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ActiveExecution> activeExecutions = new Dictionary<string, ActiveExecution>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ActionRecipe> recipeRegistry = new Dictionary<string, ActionRecipe>(StringComparer.OrdinalIgnoreCase);
        private readonly List<IStepSchedulerObserver> observers = new List<IStepSchedulerObserver>();
        private readonly object executionGate = new object();

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
                    groupResult = await ExecuteGroupAsync(group, context, state, cancellationToken).ConfigureAwait(false);
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

        private Task<StepGroupResult> ExecuteGroupAsync(
            ActionStepGroup group,
            StepSchedulerContext context,
            ExecutionState state,
            CancellationToken cancellationToken)
        {
            if (group.ExecutionMode == StepGroupExecutionMode.Sequential)
            {
                return ExecuteSequentialGroupAsync(group, context, state, cancellationToken);
            }

            if (group.JoinPolicy != StepGroupJoinPolicy.Any)
            {
                BattleLogger.Warn(LogTag, $"Parallel group '{group.Id ?? "(no id)"}' uses join '{group.JoinPolicy}'. Only 'Any' is supported in MVP; falling back to sequential execution.");
                return ExecuteSequentialGroupAsync(group, context, state, cancellationToken);
            }

            return ExecuteParallelGroupAsync(group, context, state, cancellationToken);
        }

        private async Task<StepGroupResult> ExecuteSequentialGroupAsync(
            ActionStepGroup group,
            StepSchedulerContext context,
            ExecutionState state,
            CancellationToken cancellationToken)
        {
            for (int i = 0; i < group.Steps.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var step = group.Steps[i];
                var result = await ExecuteStepAsync(step, context, state, cancellationToken, swallowCancellation: false).ConfigureAwait(false);

                if (result.Status == StepRunStatus.Branch)
                {
                    return StepGroupResult.Branch(result.BranchTargetId);
                }

                if (result.Status == StepRunStatus.Abort)
                {
                    return StepGroupResult.Abort(result.AbortReason);
                }

                if (result.Status == StepRunStatus.Failed)
                {
                    return StepGroupResult.Abort("StepFailed");
                }
            }

            return StepGroupResult.Completed();
        }

        private async Task<StepGroupResult> ExecuteParallelGroupAsync(
            ActionStepGroup group,
            StepSchedulerContext context,
            ExecutionState state,
            CancellationToken cancellationToken)
        {
            if (group.Steps.Count == 0)
            {
                return StepGroupResult.Completed();
            }

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var tasks = ListPool<Task<StepResult>>.Rent();

            try
            {
                for (int i = 0; i < group.Steps.Count; i++)
                {
                    tasks.Add(ExecuteStepAsync(group.Steps[i], context, state, linkedCts.Token, swallowCancellation: true));
                }

                if (tasks.Count == 0)
                {
                    return StepGroupResult.Completed();
                }

                var remaining = new List<Task<StepResult>>(tasks);
                StepGroupResult aggregate = StepGroupResult.Completed();

                while (remaining.Count > 0)
                {
                    var finished = await Task.WhenAny(remaining).ConfigureAwait(false);
                    remaining.Remove(finished);

                    var result = await finished.ConfigureAwait(false);

                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException(cancellationToken);
                    }

                    if (aggregate.Status != StepGroupResultStatus.Completed)
                    {
                        continue;
                    }

                    if (result.Status == StepRunStatus.Branch)
                    {
                        aggregate = StepGroupResult.Branch(result.BranchTargetId);
                        linkedCts.Cancel();
                    }
                    else if (result.Status == StepRunStatus.Abort)
                    {
                        aggregate = StepGroupResult.Abort(result.AbortReason);
                        linkedCts.Cancel();
                    }
                    else if (result.Status == StepRunStatus.Failed)
                    {
                        aggregate = StepGroupResult.Abort("StepFailed");
                        linkedCts.Cancel();
                    }
                }

                return aggregate;
            }
            finally
            {
                foreach (var task in tasks)
                {
                    if (!task.IsCompleted)
                    {
                        continue;
                    }

                    try
                    {
                        _ = task.Result;
                    }
                    catch
                    {
                        // logged at step level
                    }
                }

                ListPool<Task<StepResult>>.Return(tasks);
            }
        }

        private async Task<StepResult> ExecuteStepAsync(
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

                if (IsSystemStep(step))
                {
                    result = ExecuteSystemStep(step, schedulerContext, state);
                }
                else if (!TryResolveExecutor(step, out var executor))
                {
                    result = StepResult.Skipped;
                }
                else
                {
                    result = await ExecuteExecutorStepAsync(executor, step, schedulerContext, cancellationToken, swallowCancellation).ConfigureAwait(false);
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
                BattleLogger.Log(LogTag, $"Step '{step.Id ?? "(no id)"}' branched to '{result.BranchTargetId ?? "(null)"}'.");
            }
            else if (result.Status == StepRunStatus.Abort)
            {
                BattleLogger.Warn(LogTag, $"Step '{step.Id ?? "(no id)"}' aborted. Reason={result.AbortReason ?? "(null)"}.");
            }

            return result;
        }

        private StepResult ExecuteSystemStep(ActionStep step, StepSchedulerContext context, ExecutionState state)
        {
            string id = step.ExecutorId ?? string.Empty;
            switch (id.ToLowerInvariant())
            {
                case SystemStepWindowOpen:
                    HandleWindowOpen(step, context, state);
                    return StepResult.Completed;

                case SystemStepWindowClose:
                    HandleWindowClose(step, context, state);
                    return StepResult.Completed;

                case SystemStepGate:
                    return HandleGate(step, context, state);

                case SystemStepDamage:
                    HandleDamage(step, context);
                    return StepResult.Completed;

                case SystemStepFallback:
                    return HandleFallback(step, context);

                default:
                    BattleLogger.Warn(LogTag, $"Unknown system step '{step.ExecutorId}'. Marking as skipped.");
                    return StepResult.Skipped;
            }
        }

        private async Task<StepResult> ExecuteExecutorStepAsync(
            IActionStepExecutor executor,
            ActionStep step,
            StepSchedulerContext schedulerContext,
            CancellationToken cancellationToken,
            bool swallowCancellation)
        {
            if (!await ResolveConflictAsync(executor.Id, step.ConflictPolicy, cancellationToken).ConfigureAwait(false))
            {
                BattleLogger.Log(LogTag, $"Step '{step.Id ?? "(no id)"}' skipped due to conflict policy '{step.ConflictPolicy}'.");
                return StepResult.Skipped;
            }

            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var context = new StepExecutionContext(schedulerContext, step, linkedCts.Token);
            var executionTask = RunExecutorAsync(executor, context, linkedCts);
            RegisterActiveExecution(executor.Id, executionTask, linkedCts);

            try
            {
                await executionTask.ConfigureAwait(false);
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
                RemoveActiveExecution(executor.Id, executionTask);
            }
        }

        private void HandleWindowOpen(ActionStep step, StepSchedulerContext context, ExecutionState state)
        {
            var parameters = step.Parameters;
            if (!TryGetRequired(parameters, "id", out var id))
            {
                BattleLogger.Warn(LogTag, "window.open missing 'id'.");
                return;
            }

            string tag = parameters.TryGetString("tag", out var tagValue) ? tagValue : id;
            float start = parameters.TryGetFloat("start", out var s) ? s : parameters.TryGetFloat("startNormalized", out s) ? s : 0f;
            float end = parameters.TryGetFloat("end", out var e) ? e : parameters.TryGetFloat("endNormalized", out e) ? e : 1f;
            int index = parameters.TryGetInt("index", out var idx) ? idx : parameters.TryGetInt("windowIndex", out idx) ? idx : 0;
            int count = parameters.TryGetInt("count", out var cnt) ? cnt : parameters.TryGetInt("windowCount", out cnt) ? cnt : 1;
            string payload = BuildPayload(parameters, new[] { "id", "tag", "start", "startNormalized", "end", "endNormalized", "index", "windowIndex", "count", "windowCount" });

            state.RegisterWindow(id, new ExecutionState.WindowState(id, tag));
            context.EventBus?.Publish(new AnimationWindowEvent(context.Actor, tag, payload, start, end, true, index, count));
        }

        private void HandleWindowClose(ActionStep step, StepSchedulerContext context, ExecutionState state)
        {
            var parameters = step.Parameters;
            if (!TryGetRequired(parameters, "id", out var id))
            {
                BattleLogger.Warn(LogTag, "window.close missing 'id'.");
                return;
            }

            if (!state.TryRemoveWindow(id, out var window))
            {
                BattleLogger.Warn(LogTag, $"window.close('{id}') called but window not open.");
            }

            string tag = parameters.TryGetString("tag", out var tagValue) ? tagValue : window?.Tag ?? id;
            float start = parameters.TryGetFloat("start", out var s) ? s : parameters.TryGetFloat("startNormalized", out s) ? s : 0f;
            float end = parameters.TryGetFloat("end", out var e) ? e : parameters.TryGetFloat("endNormalized", out e) ? e : 1f;
            int index = parameters.TryGetInt("index", out var idx) ? idx : parameters.TryGetInt("windowIndex", out idx) ? idx : 0;
            int count = parameters.TryGetInt("count", out var cnt) ? cnt : parameters.TryGetInt("windowCount", out cnt) ? cnt : 1;
            string payload = BuildPayload(parameters, new[] { "id", "tag", "start", "startNormalized", "end", "endNormalized", "index", "windowIndex", "count", "windowCount" });

            context.EventBus?.Publish(new AnimationWindowEvent(context.Actor, tag, payload, start, end, false, index, count));
        }

        private StepResult HandleGate(ActionStep step, StepSchedulerContext context, ExecutionState state)
        {
            var parameters = step.Parameters;
            if (!TryGetRequired(parameters, "id", out var id))
            {
                BattleLogger.Warn(LogTag, "gate.on missing 'id'.");
                return StepResult.Failed;
            }

            string successLabel = parameters.TryGetString("success", out var success) ? success : null;
            string failLabel = parameters.TryGetString("fail", out var fail) ? fail : null;
            string timeoutLabel = parameters.TryGetString("timeout", out var timeout) ? timeout : failLabel;

            var judgments = ParseJudgmentList(parameters.TryGetString("successOn", out var list) ? list : null);
            if (!state.TryGetWindowResult(id, out var hitResult))
            {
                if (string.IsNullOrWhiteSpace(timeoutLabel))
                {
                    return StepResult.Completed;
                }

                bool abortOnTimeout = parameters.TryGetBool("abortOnTimeout", out var abortTimeout) && abortTimeout;
                return abortOnTimeout ? StepResult.Abort("GateTimeout") : StepResult.Branch(timeoutLabel);
            }

            bool isSuccess = judgments.Contains(hitResult.Judgment);
            string branchTarget = isSuccess ? successLabel : failLabel;

            if (string.IsNullOrWhiteSpace(branchTarget))
            {
                return StepResult.Completed;
            }

            bool abort =
                (isSuccess && parameters.TryGetBool("abortOnSuccess", out var abortOnSuccess) && abortOnSuccess) ||
                (!isSuccess && parameters.TryGetBool("abortOnFail", out var abortOnFail) && abortOnFail);

            if (abort)
            {
                return StepResult.Abort(isSuccess ? "GateAbortSuccess" : "GateAbortFail");
            }

            return StepResult.Branch(branchTarget);
        }

        private void HandleDamage(ActionStep step, StepSchedulerContext context)
        {
            if (!TryGetRequired(step.Parameters, "formula", out var formula))
            {
                BattleLogger.Warn(LogTag, "damage.apply missing 'formula'.");
                return;
            }

            var evt = new AnimationDamageRequestEvent(
                context.Actor,
                context.Request.Selection.Action,
                context.Request.Targets,
                formula,
                step.Parameters.Data);

            context.EventBus?.Publish(evt);
        }

        private StepResult HandleFallback(ActionStep step, StepSchedulerContext context)
        {
            string timelineId = step.Parameters.TryGetString("timelineId", out var timeline) ? timeline : null;
            string recipeId = step.Parameters.TryGetString("recipeId", out var recipe) ? recipe : null;
            string reason = step.Parameters.TryGetString("reason", out var r) ? r : "FallbackTriggered";

            if (string.IsNullOrWhiteSpace(timelineId) && string.IsNullOrWhiteSpace(recipeId))
            {
                BattleLogger.Warn(LogTag, "fallback step requires 'timelineId' or 'recipeId'.");
                return StepResult.Failed;
            }

            context.EventBus?.Publish(new AnimationFallbackRequestedEvent(context.Actor, timelineId, recipeId, reason));
            return StepResult.Abort(reason);
        }

        private static bool IsSystemStep(ActionStep step)
        {
            string id = step.ExecutorId ?? string.Empty;
            switch (id.ToLowerInvariant())
            {
                case SystemStepWindowOpen:
                case SystemStepWindowClose:
                case SystemStepGate:
                case SystemStepDamage:
                case SystemStepFallback:
                    return true;
                default:
                    return false;
            }
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

        private static string BuildPayload(ActionStepParameters parameters, IEnumerable<string> excludedKeys)
        {
            var excluded = new HashSet<string>(excludedKeys ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var list = new List<string>();

            if (!parameters.IsEmpty)
            {
                foreach (var kv in parameters.Data)
                {
                    if (excluded.Contains(kv.Key))
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(kv.Key) || string.IsNullOrWhiteSpace(kv.Value))
                    {
                        continue;
                    }

                    list.Add($"{kv.Key}={kv.Value}");
                }
            }

            return list.Count == 0 ? string.Empty : string.Join(";", list);
        }

        private static bool TryGetRequired(ActionStepParameters parameters, string key, out string value)
        {
            if (parameters.TryGetString(key, out value) && !string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            value = null;
            return false;
        }

        private static HashSet<TimedHitJudgment> ParseJudgmentList(string csv)
        {
            if (string.IsNullOrWhiteSpace(csv))
            {
                return new HashSet<TimedHitJudgment>(DefaultSuccessJudgments);
            }

            var set = new HashSet<TimedHitJudgment>();
            var tokens = csv.Split(',');
            for (int i = 0; i < tokens.Length; i++)
            {
                var token = tokens[i].Trim();
                if (Enum.TryParse<TimedHitJudgment>(token, true, out var judgment))
                {
                    set.Add(judgment);
                }
            }

            if (set.Count == 0)
            {
                return new HashSet<TimedHitJudgment>(DefaultSuccessJudgments);
            }

            return set;
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

        private async Task<bool> ResolveConflictAsync(string executorId, StepConflictPolicy policy, CancellationToken cancellationToken)
        {
            ActiveExecution active;
            lock (executionGate)
            {
                if (!activeExecutions.TryGetValue(executorId, out active))
                {
                    return true;
                }
            }

            switch (policy)
            {
                case StepConflictPolicy.SkipIfRunning:
                    return false;

                case StepConflictPolicy.CancelRunning:
                    active.Cancellation.Cancel();
                    try
                    {
                        await active.Task.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception ex)
                    {
                        BattleLogger.Warn(LogTag, $"Executor '{executorId}' threw while being cancelled: {ex.Message}");
                    }
                    return true;

                case StepConflictPolicy.WaitForCompletion:
                    try
                    {
                        await active.Task.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    return true;

                default:
                    BattleLogger.Warn(LogTag, $"Unknown conflict policy '{policy}' for executor '{executorId}'. Defaulting to wait.");
                    try
                    {
                        await active.Task.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    return true;
            }
        }

        private static async Task RunExecutorAsync(IActionStepExecutor executor, StepExecutionContext context, CancellationTokenSource linkedCts)
        {
            try
            {
                if (context.Step.HasDelay)
                {
                    var delay = TimeSpan.FromSeconds(context.Step.DelaySeconds);
                    await Task.Delay(delay, context.CancellationToken).ConfigureAwait(false);
                }

                await executor.ExecuteAsync(context).ConfigureAwait(false);
            }
            finally
            {
                linkedCts.Dispose();
            }
        }

        private void RegisterActiveExecution(string executorId, Task task, CancellationTokenSource cancellationSource)
        {
            lock (executionGate)
            {
                activeExecutions[executorId] = new ActiveExecution(task, cancellationSource);
            }
        }

        private void RemoveActiveExecution(string executorId, Task task)
        {
            lock (executionGate)
            {
                if (activeExecutions.TryGetValue(executorId, out var active) && ReferenceEquals(active.Task, task))
                {
                    activeExecutions.Remove(executorId);
                }
            }
        }

        private readonly struct ActiveExecution
        {
            public ActiveExecution(Task task, CancellationTokenSource cancellation)
            {
                Task = task;
                Cancellation = cancellation;
            }

            public Task Task { get; }
            public CancellationTokenSource Cancellation { get; }
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

    }

}

