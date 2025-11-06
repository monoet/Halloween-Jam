using System;
using System.Threading.Tasks;
using BattleV2.Actions;
using BattleV2.Charge;
using BattleV2.Execution;
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

            var handle = selection.TimedHitHandle;
            if (handle != null)
            {
                try
                {
                    var awaited = await handle.WaitAsync(context.CancellationToken).ConfigureAwait(false);
                    if (awaited.HasValue)
                    {
                        context.TimedResult = awaited.Value;
                        if (awaited.Value.Cancelled)
                        {
                            context.Cancelled = true;
                            return;
                        }
                    }
                    else
                    {
                        context.TimedResult = awaited;
                    }
                }
                catch (OperationCanceledException)
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

            var profile = selection.TimedHitProfile ?? timedHitAction?.TimedHitProfile;
            if (profile == null)
            {
                if (next != null)
                {
                    await next().ConfigureAwait(false);
                }
                return;
            }

            bool isPlayerAction = manager != null && context.Attacker != null && manager.Player == context.Attacker;
            var runner = isPlayerAction
                ? manager?.TimedHitRunner ?? InstantTimedHitRunner.Shared
                : InstantTimedHitRunner.Shared;

            var request = new TimedHitRequest(
                context.Attacker,
                context.Target,
                context.ActionData,
                selection.ChargeProfile,
                profile,
                selection.CpCharge,
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
