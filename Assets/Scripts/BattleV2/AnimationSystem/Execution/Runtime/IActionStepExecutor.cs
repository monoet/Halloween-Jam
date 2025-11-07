using System.Threading.Tasks;

namespace BattleV2.AnimationSystem.Execution.Runtime
{
    /// <summary>
    /// Strategy executed by <see cref="StepScheduler"/>. Implementations must be cancellation-aware and thread-safe.
    /// </summary>
    public interface IActionStepExecutor
    {
        /// <summary>Stable identifier used by recipes.</summary>
        string Id { get; }

        /// <summary>Returns true when the executor can run the supplied step (e.g., binding present).</summary>
        bool CanExecute(ActionStep step);

        /// <summary>
        /// Executes the step asynchronously. Implementations must honor <see cref="StepExecutionContext.CancellationToken"/>
        /// and avoid touching UnityEngine APIs outside the main thread (use <c>IMainThreadInvoker</c> when needed).
        /// </summary>
        Task ExecuteAsync(StepExecutionContext context);
    }
}
