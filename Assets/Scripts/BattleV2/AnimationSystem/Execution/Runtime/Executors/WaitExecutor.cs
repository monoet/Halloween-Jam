using System;
using System.Threading.Tasks;
using BattleV2.Core;

namespace BattleV2.AnimationSystem.Execution.Runtime.Executors
{
    /// <summary>
    /// Simple executor that waits for a given duration before completing.
    /// </summary>
    public sealed class WaitExecutor : IActionStepExecutor
    {
        public const string ExecutorId = "wait";
        private const string LogScope = "AnimStep/Wait";

        public string Id => ExecutorId;

        public bool CanExecute(ActionStep step)
        {
            return true;
        }

        public async Task ExecuteAsync(StepExecutionContext context)
        {
            float seconds = 0f;
            var parameters = context.Step.Parameters;
            if (parameters.TryGetFloat("seconds", out var secondsValue))
            {
                seconds = secondsValue;
            }
            else if (parameters.TryGetFloat("duration", out var durationValue))
            {
                seconds = durationValue;
            }
            else if (parameters.TryGetFloat("milliseconds", out var milliseconds))
            {
                seconds = milliseconds / 1000f;
            }

            seconds = Math.Max(0f, seconds);

            if (seconds <= 0f)
            {
                return;
            }

            try
            {
                var delay = TimeSpan.FromSeconds(seconds);
                await Task.Delay(delay, context.CancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelled upstream.
            }
        }
    }
}
