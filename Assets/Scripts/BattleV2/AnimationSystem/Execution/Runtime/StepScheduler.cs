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

        public StepScheduler()
        {
            systemStepRunner = new SystemStepRunner(LogTag);
            sequentialRunner = new SequentialGroupRunner(this);
            parallelRunner = new ParallelGroupRunner(this);
            activeExecutionRegistry = new ActiveExecutionRegistry(LogTag);
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
                return sequentialRunner.ExecuteAsync(group, context, state, cancellationToken);
            }

            if (group.JoinPolicy != StepGroupJoinPolicy.Any)
            {
                BattleLogger.Warn(LogTag, $"Parallel group '{group.Id ?? "(no id)"}' uses join '{group.JoinPolicy}'. Only 'Any' is supported in MVP; falling back to sequential execution.");
                return sequentialRunner.ExecuteAsync(group, context, state, cancellationToken);
            }

            return parallelRunner.ExecuteAsync(group, context, state, cancellationToken);
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
            if (!await activeExecutionRegistry.ResolveConflictAsync(executor.Id, step.ConflictPolicy, cancellationToken).ConfigureAwait(false))
            {
                BattleLogger.Log(LogTag, $"Step '{step.Id ?? "(no id)"}' skipped due to conflict policy '{step.ConflictPolicy}'.");
                return StepResult.Skipped;
            }

            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var context = new StepExecutionContext(schedulerContext, step, linkedCts.Token);
            var executionTask = RunExecutorAsync(executor, context, linkedCts);
            activeExecutionRegistry.Register(executor.Id, executionTask, linkedCts);

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
                activeExecutionRegistry.Remove(executor.Id, executionTask);
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


