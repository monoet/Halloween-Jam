using System;
using System.Collections.Generic;
using BattleV2.AnimationSystem;
using BattleV2.Core;

namespace BattleV2.AnimationSystem.Execution.Routers
{
    /// <summary>
    /// Listens to impact events and forwards the payload to the visual effects service.
    /// </summary>
    public sealed class AnimationVfxRouter : IDisposable
    {
        private const string LogScope = "AnimVFX";

        private readonly IAnimationEventBus eventBus;
        private readonly IAnimationVfxService vfxService;
        private readonly List<IDisposable> subscriptions = new();
        private readonly Dictionary<CombatantState, HashSet<string>> activeEffects = new();

        private bool disposed;

        public AnimationVfxRouter(IAnimationEventBus eventBus, IAnimationVfxService vfxService)
        {
            this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            this.vfxService = vfxService ?? throw new ArgumentNullException(nameof(vfxService));

            subscriptions.Add(this.eventBus.Subscribe<AnimationImpactEvent>(OnImpact));
            subscriptions.Add(this.eventBus.Subscribe<AnimationLockEvent>(OnLock));
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            for (int i = 0; i < subscriptions.Count; i++)
            {
                subscriptions[i]?.Dispose();
            }

            subscriptions.Clear();
            activeEffects.Clear();
        }

        private void OnImpact(AnimationImpactEvent evt)
        {
            if (disposed)
            {
                return;
            }

            var payload = AnimationEventPayload.Parse(evt.Payload);
            string vfxId = payload.ResolveId("vfx", "effect", "id");
            if (string.IsNullOrWhiteSpace(vfxId))
            {
                BattleLogger.Warn(LogScope, $"Impact payload missing VFX id (action={evt.Action?.id ?? "(none)"}, tag={evt.Tag ?? "(null)"}). Payload='{payload}'.");
                return;
            }

            bool played = vfxService.TryPlay(vfxId, evt, payload);
            if (!played)
            {
                BattleLogger.Warn(LogScope, $"Unable to play VFX '{vfxId}' for action '{evt.Action?.id ?? "(none)"}'. Check bindings and assets.");
                return;
            }

            TrackEffect(evt.Actor, vfxId);
        }

        private void OnLock(AnimationLockEvent evt)
        {
            if (evt.Actor == null || evt.IsLocked)
            {
                return;
            }

            if (activeEffects.TryGetValue(evt.Actor, out var set))
            {
                set.Clear();
            }

            vfxService.StopAllFor(evt.Actor);
        }

        private void TrackEffect(CombatantState actor, string vfxId)
        {
            if (actor == null || string.IsNullOrWhiteSpace(vfxId))
            {
                return;
            }

            if (!activeEffects.TryGetValue(actor, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                activeEffects[actor] = set;
            }

            set.Add(vfxId);
        }
    }
}
