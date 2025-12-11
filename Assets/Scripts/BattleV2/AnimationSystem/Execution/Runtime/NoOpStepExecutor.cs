using System.Threading.Tasks;

namespace BattleV2.AnimationSystem.Execution.Runtime
{
    /// <summary>
    /// Simple executor that does nothing. Used for synthetic recipes (e.g., auto run_back) to ensure
    /// scheduler notifies observers even when no real steps are authored.
    /// </summary>
    public sealed class NoOpStepExecutor : IActionStepExecutor
    {
        public const string ExecutorId = "noop";
        public string Id => ExecutorId;

        public bool CanExecute(ActionStep step) => true;

        public Task ExecuteAsync(StepExecutionContext context) => Task.CompletedTask;
    }
}
