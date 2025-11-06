using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BattleV2.AnimationSystem.Execution.Runtime;
using BattleV2.AnimationSystem.Execution.Runtime.Core;

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
                        // Step result already logged at scheduler level.
                    }
                }

                ListPool<Task<StepResult>>.Return(tasks);
            }
        }
    }
}
