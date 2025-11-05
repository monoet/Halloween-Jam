using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using BattleV2.AnimationSystem;
using BattleV2.Core;
using BattleV2.AnimationSystem.Execution.Runtime.SystemSteps;

namespace BattleV2.AnimationSystem.Execution.Runtime
{
    /// <summary>
    /// Coordinates execution of action recipes by delegating steps to registered executors.
    /// </summary>
    public sealed class StepScheduler
    {
        private const string LogTag = "StepScheduler";

        private readonly Dictionary<string, IActionStepExecutor> executors = new Dictionary<string, IActionStepExecutor>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ActiveExecution> activeExecutions = new Dictionary<string, ActiveExecution>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ActionRecipe> recipeRegistry = new Dictionary<string, ActionRecipe>(StringComparer.OrdinalIgnoreCase);
        private readonly List<IStepSchedulerObserver> observers = new List<IStepSchedulerObserver>();
        private readonly object executionGate = new object();
        private readonly SystemStepRunner systemStepRunner = new SystemStepRunner(LogTag);

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

            using var state = new ExecutionState(recipe, context);
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

        private enum StepRunStatus
        {
            Completed,
            Skipped,
            Failed,
            Branch,
            Abort
        }

        private enum StepGroupResultStatus
        {
            Completed,
            Branch,
            Abort
        }

        internal readonly struct StepResult
        {
            private StepResult(StepRunStatus status, string branchTargetId, string abortReason)
            {
                Status = status;
                BranchTargetId = branchTargetId;
                AbortReason = abortReason;
            }

            public StepRunStatus Status { get; }
            public string BranchTargetId { get; }
            public string AbortReason { get; }

            public static StepResult Completed => new(StepRunStatus.Completed, null, null);
            public static StepResult Skipped => new(StepRunStatus.Skipped, null, null);
            public static StepResult Failed => new(StepRunStatus.Failed, null, null);
            public static StepResult Branch(string targetId) => new(StepRunStatus.Branch, targetId, null);
            public static StepResult Abort(string reason) => new(StepRunStatus.Abort, null, reason);
        }

        private readonly struct StepGroupResult
        {
            private StepGroupResult(StepGroupResultStatus status, string branchTargetId, string abortReason)
            {
                Status = status;
                BranchTargetId = branchTargetId;
                AbortReason = abortReason;
            }

            public StepGroupResultStatus Status { get; }
            public string BranchTargetId { get; }
            public string AbortReason { get; }

            public static StepGroupResult Completed() => new(StepGroupResultStatus.Completed, null, null);
            public static StepGroupResult Branch(string targetId) => new(StepGroupResultStatus.Branch, targetId, null);
            public static StepGroupResult Abort(string reason) => new(StepGroupResultStatus.Abort, null, reason);
        }

        internal sealed class ExecutionState : IDisposable
        {
            private readonly Dictionary<string, WindowState> openWindows = new(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, TimedHitResultEvent> windowResults = new(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, int> groupLookup = new(StringComparer.OrdinalIgnoreCase);
            private readonly List<IDisposable> subscriptions = new();
            private readonly StepSchedulerContext context;
            private readonly ActionRecipe recipe;

            public ExecutionState(ActionRecipe recipe, StepSchedulerContext context)
            {
                this.recipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
                this.context = context;

                if (recipe.Groups != null)
                {
                    for (int i = 0; i < recipe.Groups.Count; i++)
                    {
                        var id = recipe.Groups[i].Id;
                        if (!string.IsNullOrWhiteSpace(id) && !groupLookup.ContainsKey(id))
                        {
                            groupLookup[id] = i;
                        }
                    }
                }
            }

            public bool AbortRequested { get; private set; }

            public void Initialize()
            {
                if (context.EventBus != null)
                {
                    subscriptions.Add(context.EventBus.Subscribe<TimedHitResultEvent>(OnTimedHitResult));
                }
            }

            public void RequestAbort(string reason)
            {
                AbortRequested = true;
                BattleLogger.Warn(LogTag, $"Recipe '{recipe.Id}' aborted. Reason={reason ?? "(null)"}");
            }

            public bool TryGetGroupIndex(string id, out int index)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    index = -1;
                    return false;
                }

                return groupLookup.TryGetValue(id, out index);
            }

            public void RegisterWindow(string id, WindowState window)
            {
                if (string.IsNullOrWhiteSpace(id) || window == null)
                {
                    return;
                }

                openWindows[id] = window;
            }

            public bool TryRemoveWindow(string id, out WindowState window)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    window = null;
                    return false;
                }

                return openWindows.Remove(id, out window);
            }

            public bool TryGetWindowResult(string id, out TimedHitResultEvent result)
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    result = default;
                    return false;
                }

                return windowResults.Remove(id, out result);
            }

            public void ImmediateCleanup()
            {
                CleanupWindows();
            }

            private void OnTimedHitResult(TimedHitResultEvent evt)
            {
                if (evt.Actor != context.Actor)
                {
                    return;
                }

                windowResults[evt.Tag] = evt;
            }

            public void Dispose()
            {
                CleanupWindows();

                for (int i = 0; i < subscriptions.Count; i++)
                {
                    subscriptions[i]?.Dispose();
                }

                subscriptions.Clear();

                if (context.TimedHitService != null && context.Actor != null)
                {
                    context.TimedHitService.Reset(context.Actor);
                }
            }

            private void CleanupWindows()
            {
                if (context.EventBus != null)
                {
                    foreach (var window in openWindows.Values)
                    {
                        context.EventBus.Publish(new AnimationWindowEvent(context.Actor, window.Tag, string.Empty, 0f, 0f, false, 0, 0));
                    }
                }

                openWindows.Clear();
            }

            public sealed class WindowState
            {
                public WindowState(string id, string tag)
                {
                    Id = id;
                    Tag = tag;
                }

                public string Id { get; }
                public string Tag { get; }
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

        private static class ListPool<T>
        {
            [ThreadStatic] private static Stack<List<T>> pool;

            public static List<T> Rent()
            {
                pool ??= new Stack<List<T>>(4);
                if (pool.Count == 0)
                {
                    return new List<T>();
                }

                var list = pool.Pop();
                list.Clear();
                return list;
            }

            public static void Return(List<T> list)
            {
                if (list == null)
                {
                    return;
                }

                list.Clear();
                pool ??= new Stack<List<T>>(4);
                pool.Push(list);
            }
        }
    }

}

