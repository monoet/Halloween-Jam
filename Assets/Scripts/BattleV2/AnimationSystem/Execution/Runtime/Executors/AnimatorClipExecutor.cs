using System.Threading.Tasks;
using BattleV2.Orchestration.Runtime;

namespace BattleV2.AnimationSystem.Execution.Runtime.Executors
{
    public sealed class AnimatorClipExecutor : IActionStepExecutor
    {
        public const string ExecutorId = "anim";
        public string Id => ExecutorId;

        public bool CanExecute(ActionStep step) => true;

        public Task ExecuteAsync(StepExecutionContext context)
        {
            return Task.CompletedTask; // NO-OP
        }
    }
}
