using System;
using System.Threading.Tasks;
using BattleV2.Actions;
using BattleV2.Orchestration;

namespace BattleV2.Execution.TimedHits
{
    public sealed class TimedHitMiddleware : IActionMiddleware
    {
        private readonly BattleManagerV2 manager;
        private readonly ITimedHitAction timedHitAction;

        public TimedHitMiddleware(BattleManagerV2 manager, ITimedHitAction timedHitAction)
        {
            this.manager = manager;
            this.timedHitAction = timedHitAction;
        }

        public async Task InvokeAsync(ActionContext context, Func<Task> next)
        {
            var selection = context.Selection;
            if (selection.TimedHitResult.HasValue)
            {
                var resolved = selection.TimedHitResult.Value;
                context.TimedResult = resolved;

                if (resolved.Cancelled)
                {
                    context.Cancelled = true;
                    return;
                }

                if (next != null)
                {
                    await next().ConfigureAwait(false);
                }
                return;
            }

            if (next != null)
            {
                await next().ConfigureAwait(false);
            }
        }
    }
}
