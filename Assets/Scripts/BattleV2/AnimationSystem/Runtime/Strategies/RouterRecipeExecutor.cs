using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using BattleV2.AnimationSystem.Execution.Routers;
using BattleV2.AnimationSystem.Runtime.Internal;
using BattleV2.Core;

namespace BattleV2.AnimationSystem.Strategies
{
    internal sealed class RouterRecipeExecutor : IRecipeExecutor
    {
        private const string Prefix = "router:";

        private readonly AnimationRouterBundle routerBundle;
        private readonly IMainThreadInvoker mainThreadInvoker;

        public RouterRecipeExecutor(AnimationRouterBundle routerBundle, IMainThreadInvoker mainThreadInvoker)
        {
            this.routerBundle = routerBundle ?? throw new ArgumentNullException(nameof(routerBundle));
            this.mainThreadInvoker = mainThreadInvoker ?? throw new ArgumentNullException(nameof(mainThreadInvoker));
        }

        public bool CanExecute(string recipeId, StrategyContext context)
        {
            return !string.IsNullOrWhiteSpace(recipeId) &&
                   recipeId.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);
        }

        public async Task ExecuteAsync(string recipeId, StrategyContext context, CancellationToken token = default)
        {
            if (!CanExecute(recipeId, context))
            {
                return;
            }

            var remainder = recipeId.Substring(Prefix.Length);
            var separatorIndex = remainder.IndexOf(':');
            if (separatorIndex < 0)
            {
                context?.LogWarn($"Router recipe '{recipeId}' is missing channel information.");
                return;
            }

            var channel = remainder.Substring(0, separatorIndex).Trim();
            var payloadSlice = remainder.Substring(separatorIndex + 1);
            var payload = BuildPayload(payloadSlice);
            var effectId = ResolveEffectId(channel, payload);
            if (string.IsNullOrWhiteSpace(effectId))
            {
                context?.LogWarn($"Router recipe '{recipeId}' does not contain a valid effect identifier.");
                return;
            }

            bool handled = true;
            await mainThreadInvoker.RunAsync(() =>
            {
                handled = Dispatch(channel, effectId, payload, context?.AnimationContext.PrimaryActor);
                return Task.CompletedTask;
            });
            if (!handled)
            {
                context?.LogWarn($"Router recipe '{recipeId}' was not handled (channel='{channel}', effect='{effectId}').");
            }
        }

        private bool Dispatch(string channel, string effectId, AnimationEventPayload payload, CombatantState actor)
        {
            switch (channel.ToLower(CultureInfo.InvariantCulture))
            {
                case "camera":
                    return routerBundle.CameraService != null &&
                        routerBundle.CameraService.TryApply(effectId, actor, null, null, payload);

                case "ui":
                    return routerBundle.UiService != null &&
                        routerBundle.UiService.TryHandle(effectId, actor, null, null, null, payload);

                case "vfx":
                    return routerBundle.VfxService != null &&
                        routerBundle.VfxService.TryPlay(effectId, BuildImpactEvent(actor, payload), payload);

                case "sfx":
                    return routerBundle.SfxService != null &&
                        routerBundle.SfxService.TryPlay(effectId, actor, null, null, payload);

                default:
                    return false;
            }
        }

        private static AnimationEventPayload BuildPayload(string data)
        {
            if (string.IsNullOrWhiteSpace(data))
            {
                return default;
            }

            if (data.Contains("=") || data.Contains(";") || data.Contains(","))
            {
                return AnimationEventPayload.Parse(data);
            }

            return AnimationEventPayload.Parse($"id={data}");
        }

        private static string ResolveEffectId(string channel, AnimationEventPayload payload)
        {
            if (payload.Equals(default(AnimationEventPayload)))
            {
                return null;
            }

            return payload.ResolveId("id", "effect", channel, "target");
        }

        private static AnimationImpactEvent BuildImpactEvent(CombatantState actor, AnimationEventPayload payload)
        {
            return new AnimationImpactEvent(
                actor,
                null,
                null,
                0,
                1,
                payload.Identifier,
                payload.Raw);
        }
    }
}
