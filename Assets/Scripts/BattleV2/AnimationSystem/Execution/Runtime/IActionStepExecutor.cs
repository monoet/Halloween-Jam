using System.Threading.Tasks;

namespace BattleV2.AnimationSystem.Execution.Runtime
{
    public interface IActionStepExecutor
    {
        string Id { get; }

        bool CanExecute(ActionStep step);

        Task ExecuteAsync(StepExecutionContext context);
    }
}
