using System.Threading;
using System.Threading.Tasks;
using BattleV2.AnimationSystem.Execution.Runtime;
using BattleV2.AnimationSystem.Execution.Runtime.Core;

namespace BattleV2.AnimationSystem.Execution.Runtime.Core.GroupRunners
{
    internal sealed class SequentialGroupRunner
    {
        private readonly StepScheduler scheduler;

        public SequentialGroupRunner(StepScheduler scheduler)
        {
            this.scheduler = scheduler;
        }

        public async Task<StepGroupResult> ExecuteAsync(
            ActionStepGroup group,
            StepSchedulerContext context,
            ExecutionState state,
            CancellationToken cancellationToken)
        {
            for (int i = 0; i < group.Steps.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var step = group.Steps[i];

                var result = await scheduler.ExecuteStepInternalAsync(step, context, state, cancellationToken, swallowCancellation: false).ConfigureAwait(false);

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
    }
}
