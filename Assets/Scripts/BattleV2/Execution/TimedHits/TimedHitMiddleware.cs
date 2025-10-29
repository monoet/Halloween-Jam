using System;
using System.Threading.Tasks;
using BattleV2.Actions;
using BattleV2.Orchestration;
using BattleV2.Charge;
using BattleV2.Debugging;
using UnityEngine;

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

            var profile = timedHitAction?.TimedHitProfile;
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

            string profileName = profile != null ? profile.name : "(null)";
            string runnerName = runner != null ? runner.GetType().Name : "(null)";

            var request = new TimedHitRequest(
                context.Attacker,
                context.Target,
                context.ActionData,
                context.Selection.ChargeProfile,
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

            UnityEngine.Debug.Log($"[TimedHitMiddleware] Runner={runnerName} CpCharge={context.CpCharge} Profile={profileName}");

            context.TimedResult = result;

            if (next != null)
            {
                await next().ConfigureAwait(false);
            }
        }
    }
}
