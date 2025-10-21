using System;
using System.Threading.Tasks;
using BattleV2.Actions;
using BattleV2.Orchestration;
using BattleV2.Charge;

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
            var profile = timedHitAction?.TimedHitProfile;
            if (profile == null)
            {
                if (next != null)
                {
                    await next().ConfigureAwait(false);
                }
                return;
            }

            var runner = manager?.TimedHitRunner ?? InstantTimedHitRunner.Shared;
            var request = new TimedHitRequest(
                context.Attacker,
                context.Target,
                profile,
                context.CpCharge,
                TimedHitRunMode.Execute,
                context.CancellationToken);

            TimedHitResult result;
            try
            {
                result = await runner.RunAsync(request).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                context.Cancelled = true;
                return;
            }

            if (result.Cancelled)
            {
                context.Cancelled = true;
                return;
            }

            context.TimedResult = result;

            if (next != null)
            {
                await next().ConfigureAwait(false);
            }
        }
    }
}
