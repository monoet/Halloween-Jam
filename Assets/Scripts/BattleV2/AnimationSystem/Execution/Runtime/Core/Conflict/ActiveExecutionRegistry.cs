using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BattleV2.Core;

namespace BattleV2.AnimationSystem.Execution.Runtime.Core.Conflict
{
    internal sealed class ActiveExecutionRegistry
    {
        private readonly Dictionary<string, ActiveExecution> activeExecutions = new(StringComparer.OrdinalIgnoreCase);
        private readonly object sync = new();
        private readonly string logTag;

        public ActiveExecutionRegistry(string logTag)
        {
            this.logTag = string.IsNullOrWhiteSpace(logTag) ? "StepScheduler" : logTag;
        }

        public void Register(string executorId, Task task, CancellationTokenSource cancellationSource)
        {
            if (string.IsNullOrWhiteSpace(executorId) || task == null || cancellationSource == null)
            {
                return;
            }

            lock (sync)
            {
                activeExecutions[executorId] = new ActiveExecution(task, cancellationSource);
            }
        }

        public void Remove(string executorId, Task task)
        {
            if (string.IsNullOrWhiteSpace(executorId) || task == null)
            {
                return;
            }

            lock (sync)
            {
                if (activeExecutions.TryGetValue(executorId, out var active) && ReferenceEquals(active.Task, task))
                {
                    activeExecutions.Remove(executorId);
                }
            }
        }

        public async Task<bool> ResolveConflictAsync(string executorId, StepConflictPolicy policy, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(executorId))
            {
                return true;
            }

            ActiveExecution active;
            lock (sync)
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
                        BattleLogger.Warn(logTag, $"Executor '{executorId}' threw while being cancelled: {ex.Message}");
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
                    BattleLogger.Warn(logTag, $"Unknown conflict policy '{policy}' for executor '{executorId}'. Defaulting to wait.");
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
    }
}
