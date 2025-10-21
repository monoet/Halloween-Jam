using System;
using System.Threading.Tasks;
using BattleV2.Core;

namespace BattleV2.Execution.TimedHits
{
    public sealed class PhaseDamageMiddleware : IActionMiddleware
    {
        private readonly ITimedHitRunner runner;

        public PhaseDamageMiddleware(ITimedHitRunner runner)
        {
            this.runner = runner;
        }

        public async Task InvokeAsync(ActionContext context, Func<Task> next)
        {
            if (runner == null)
            {
                if (next != null)
                {
                    await next().ConfigureAwait(false);
                }
                return;
            }

            void OnPhaseResolved(TimedHitPhaseResult result)
            {
                // TODO: Apply damage/feedback per phase in future step.
            }

            runner.OnPhaseResolved += OnPhaseResolved;
            try
            {
                if (next != null)
                {
                    await next().ConfigureAwait(false);
                }
            }
            finally
            {
                runner.OnPhaseResolved -= OnPhaseResolved;
            }
        }
    }
}
