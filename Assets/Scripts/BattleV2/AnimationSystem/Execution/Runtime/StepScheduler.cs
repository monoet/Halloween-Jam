using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly Dictionary<string, ActiveExecution> activeExecutions = new Dictionary<string, ActiveExecution>(StringComparer.OrdinalIgnoreCase);
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

        public async Task ExecuteAsync(ActionRecipe recipe, StepSchedulerContext context, CancellationToken cancellationToken = default)
        {
            if (recipe == null || recipe.IsEmpty)
            {
                return;
            }

            for (int groupIndex = 0; groupIndex < recipe.Groups.Count; groupIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var group = recipe.Groups[groupIndex];

                if (group.ExecutionMode == StepGroupExecutionMode.Sequential)
                {
                    for (int i = 0; i < group.Steps.Count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var step = group.Steps[i];
                        if (!TryResolveExecutor(step, out var executor))
                        {
                            continue;
                        }

                        await ExecuteStepAsync(executor, step, context, cancellationToken).ConfigureAwait(false);
                    }
                }
                else
                {
                    var tasks = ListPool<Task>.Rent();
                    try
                    {
                        for (int i = 0; i < group.Steps.Count; i++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var step = group.Steps[i];
                            if (!TryResolveExecutor(step, out var executor))
                            {
                                continue;
                            }

                            tasks.Add(ExecuteStepAsync(executor, step, context, cancellationToken));
                        }

                        if (tasks.Count > 0)
                        {
                            await Task.WhenAll(tasks).ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        ListPool<Task>.Return(tasks);
                    }
                }
            }
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

        private async Task ExecuteStepAsync(IActionStepExecutor executor, ActionStep step, StepSchedulerContext schedulerContext, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!await ResolveConflictAsync(executor.Id, step.ConflictPolicy, cancellationToken).ConfigureAwait(false))
            {
                BattleLogger.Log(LogTag, $"Step '{step.Id ?? "(no id)"}' skipped due to conflict policy '{step.ConflictPolicy}'.");
                return;
            }

            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var context = new StepExecutionContext(schedulerContext, step, linkedCts.Token);

            var executionTask = RunExecutorAsync(executor, context, linkedCts);
            RegisterActiveExecution(executor.Id, executionTask, linkedCts);

            try
            {
                await executionTask.ConfigureAwait(false);
            }
            finally
            {
                RemoveActiveExecution(executor.Id, executionTask);
            }
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
                        // Expected when the previous step observes cancellation.
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
                        // Previous step was cancelled; continue.
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
            catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
            {
                // Swallow expected cancellation.
            }
            catch (Exception ex)
            {
                BattleLogger.Error(LogTag, $"Executor '{executor.Id}' crashed while running step '{context.Step.Id ?? "(no id)"}': {ex}");
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
