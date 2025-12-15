using System;
using System.Threading.Tasks;

namespace BattleV2.AnimationSystem.Execution.Runtime.Executors
{
    /// <summary>
    /// Simple executor that waits for a literal duration specified on the step.
    /// </summary>
    public sealed class WaitSecondsExecutor : IActionStepExecutor
    {
        public const string ExecutorId = "wait_seconds";
        public string Id => ExecutorId;

        public bool CanExecute(ActionStep step) => true;

        public async Task ExecuteAsync(StepExecutionContext ctx)
        {
            var seconds = ResolveDuration(ctx.Step);
            if (seconds <= 0f)
            {
                return;
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(seconds), ctx.CancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Swallow expected cancellation so the scheduler can continue its teardown flow.
            }
        }

        private static float ResolveDuration(ActionStep step)
        {
            var parameters = step.Parameters;
            if (parameters.TryGetFloat("seconds", out var bySeconds))
            {
                return Math.Max(0f, bySeconds);
            }

            if (parameters.TryGetFloat("duration", out var byDuration))
            {
                return Math.Max(0f, byDuration);
            }

            if (parameters.TryGetFloat("value", out var genericValue))
            {
                return Math.Max(0f, genericValue);
            }

            return 0f;
        }
    }
}
