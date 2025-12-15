using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BattleV2.AnimationSystem.Execution.Runtime.Core;
using BattleV2.Core;

namespace BattleV2.AnimationSystem.Execution.Runtime.Core.GroupRunners
{
    internal sealed class ParallelGroupRunner
    {
        private readonly StepScheduler scheduler;

        public ParallelGroupRunner(StepScheduler scheduler)
        {
            this.scheduler = scheduler;
        }

        public async Task<StepGroupResult> ExecuteAsync(
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
                    var step = group.Steps[i];
                    tasks.Add(scheduler.ExecuteStepInternalAsync(step, context, state, linkedCts.Token, swallowCancellation: true));
                }

                if (tasks.Count == 0)
                {
                    return StepGroupResult.Completed();
                }

                var timeoutTask = group.HasTimeout
                    ? Task.Delay(TimeSpan.FromSeconds(group.TimeoutSeconds), cancellationToken)
                    : null;

                if (group.JoinPolicy == StepGroupJoinPolicy.Any)
                {
                    return await ExecuteJoinAnyAsync(tasks, timeoutTask, linkedCts, state, cancellationToken);
                }

                return await ExecuteJoinAllAsync(tasks, timeoutTask, linkedCts, state, cancellationToken);
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
                        // Step result already logged at scheduler level.
                    }
                }

                ListPool<Task<StepResult>>.Return(tasks);
            }
        }

        private async Task<StepGroupResult> ExecuteJoinAnyAsync(
            List<Task<StepResult>> tasks,
            Task timeoutTask,
            CancellationTokenSource linkedCts,
            ExecutionState state,
            CancellationToken cancellationToken)
        {
            var pending = new List<Task<StepResult>>(tasks);

            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Task completedTask;
                if (timeoutTask != null)
                {
                    var waitArray = CreateWaitArray(pending, timeoutTask);
                    completedTask = await Task.WhenAny(waitArray);
                }
                else
                {
                    completedTask = await Task.WhenAny(pending);
                }

                if (timeoutTask != null && ReferenceEquals(completedTask, timeoutTask))
                {
                    linkedCts.Cancel();
                    await DrainPendingAsync(pending);
                    state.ImmediateCleanup();
                    return StepGroupResult.Abort("ParallelTimeout");
                }

                var finished = (Task<StepResult>)completedTask;
                pending.Remove(finished);

                var result = await finished;

                if (result.Status == StepRunStatus.Branch && !string.IsNullOrWhiteSpace(result.BranchTargetId))
                {
                    linkedCts.Cancel();
                    await DrainPendingAsync(pending);
                    return StepGroupResult.Branch(result.BranchTargetId);
                }

                if (result.Status == StepRunStatus.Abort)
                {
                    linkedCts.Cancel();
                    await DrainPendingAsync(pending);
                    state.ImmediateCleanup();
                    return StepGroupResult.Abort(result.AbortReason ?? "ParallelAbort");
                }

                if (result.Status == StepRunStatus.Failed)
                {
                    linkedCts.Cancel();
                    await DrainPendingAsync(pending);
                    state.ImmediateCleanup();
                    return StepGroupResult.Abort("StepFailed");
                }
            }

            return StepGroupResult.Completed();
        }

        private async Task<StepGroupResult> ExecuteJoinAllAsync(
            List<Task<StepResult>> tasks,
            Task timeoutTask,
            CancellationTokenSource linkedCts,
            ExecutionState state,
            CancellationToken cancellationToken)
        {
            var whenAll = Task.WhenAll(tasks);
            Task completedTask = timeoutTask != null
                ? await Task.WhenAny(whenAll, timeoutTask)
                : await Task.WhenAny(whenAll);

            if (timeoutTask != null && ReferenceEquals(completedTask, timeoutTask))
            {
                linkedCts.Cancel();
                await DrainPendingAsync(tasks);
                state.ImmediateCleanup();
                return StepGroupResult.Abort("ParallelTimeout");
            }

            var results = await whenAll;

            string branchTarget = null;
            for (int i = 0; i < results.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = results[i];
                if (result.Status == StepRunStatus.Abort)
                {
                    state.ImmediateCleanup();
                    return StepGroupResult.Abort(result.AbortReason ?? "ParallelAbort");
                }

                if (result.Status == StepRunStatus.Failed)
                {
                    state.ImmediateCleanup();
                    return StepGroupResult.Abort("StepFailed");
                }

                if (result.Status == StepRunStatus.Branch)
                {
                    if (string.IsNullOrWhiteSpace(branchTarget))
                    {
                        branchTarget = result.BranchTargetId;
                    }
                    else if (!string.Equals(branchTarget, result.BranchTargetId, StringComparison.OrdinalIgnoreCase))
                    {
                        BattleLogger.Warn("StepScheduler/Parallel", $"Parallel group emitted conflicting branch targets '{branchTarget}' and '{result.BranchTargetId ?? "(null)"}'.");
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(branchTarget))
            {
                return StepGroupResult.Branch(branchTarget);
            }

            return StepGroupResult.Completed();
        }

        private static Task[] CreateWaitArray(List<Task<StepResult>> pending, Task timeoutTask)
        {
            var waitArray = new Task[pending.Count + 1];
            for (int i = 0; i < pending.Count; i++)
            {
                waitArray[i] = pending[i];
            }

            waitArray[waitArray.Length - 1] = timeoutTask;
            return waitArray;
        }

        private static async Task DrainPendingAsync(List<Task<StepResult>> tasks)
        {
            if (tasks == null || tasks.Count == 0)
            {
                return;
            }

            try
            {
                await Task.WhenAll(tasks);
            }
            catch
            {
                // Individual step failures are handled by the scheduler; nothing else to do here.
            }
        }
    }
}
