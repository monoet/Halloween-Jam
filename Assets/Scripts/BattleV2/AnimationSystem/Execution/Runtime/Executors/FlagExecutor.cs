using System.Threading.Tasks;
using BattleV2.Orchestration.Runtime;

namespace BattleV2.AnimationSystem.Execution.Runtime.Executors
{
    /// <summary>
    /// Executor mínimo que emite flags al SchedulerFlagBus.
    /// </summary>
    public sealed class FlagExecutor : IActionStepExecutor
    {
        public const string ExecutorId = "flag";
        public string Id => ExecutorId;

        public bool CanExecute(ActionStep step) => true;

        public Task ExecuteAsync(StepExecutionContext context)
        {
            StepSchedulerFlagBus.Emit(context.Step.BindingId ?? context.Step.Id);
            return Task.CompletedTask;
        }
    }
}
