using System;
using System.Collections.Generic;
using BattleV2.AnimationSystem;
using BattleV2.Core;

namespace BattleV2.AnimationSystem.Execution.Routers
{
    /// <summary>
    /// Dispatches soundtrack payloads to the configured SFX service.
    /// </summary>
    public sealed class AnimationSfxRouter : IDisposable
    {
        private const string LogScope = "AnimSFX";

        private readonly IAnimationEventBus eventBus;
        private readonly IAnimationSfxService sfxService;
        private readonly List<IDisposable> subscriptions = new();
        private readonly HashSet<CombatantState> actorsWithActiveAudio = new();

        private bool disposed;

        public AnimationSfxRouter(IAnimationEventBus eventBus, IAnimationSfxService sfxService)
        {
            this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            this.sfxService = sfxService ?? throw new ArgumentNullException(nameof(sfxService));

            subscriptions.Add(this.eventBus.Subscribe<AnimationImpactEvent>(OnImpact));
            subscriptions.Add(this.eventBus.Subscribe<AnimationPhaseEvent>(OnPhase));
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
            actorsWithActiveAudio.Clear();
        }

        private void OnImpact(AnimationImpactEvent evt)
        {
            ProcessPayload(evt.Actor, evt.Payload, evt, null);
        }

        private void OnPhase(AnimationPhaseEvent evt)
        {
            ProcessPayload(evt.Actor, evt.Payload, null, evt);
        }

        private void ProcessPayload(
            CombatantState actor,
            string rawPayload,
            AnimationImpactEvent? impact,
            AnimationPhaseEvent? phase)
        {
            if (disposed || string.IsNullOrWhiteSpace(rawPayload))
            {
                return;
            }

            var payload = AnimationEventPayload.Parse(rawPayload);
            string sfxId = payload.ResolveId("sfx", "sound", "clip", "id");
            if (string.IsNullOrWhiteSpace(sfxId))
            {
                if (impact.HasValue || phase.HasValue)
                {
                    string source = impact.HasValue ? "impact" : "phase";
                    BattleLogger.Warn(LogScope, $"Missing SFX id in {source} payload (actor={actor?.name ?? "(null)"}). Payload='{payload}'.");
                }
                return;
            }

            bool played = sfxService.TryPlay(sfxId, actor, impact, phase, payload);
            if (!played)
            {
                BattleLogger.Warn(LogScope, $"Unable to play SFX '{sfxId}' (actor={actor?.name ?? "(null)"}). Verify audio bindings.");
                return;
            }

            if (actor != null)
            {
                actorsWithActiveAudio.Add(actor);
            }
        }

        private void OnLock(AnimationLockEvent evt)
        {
            if (evt.Actor == null || evt.IsLocked || !actorsWithActiveAudio.Contains(evt.Actor))
            {
                return;
            }

            sfxService.StopAllFor(evt.Actor);
            actorsWithActiveAudio.Remove(evt.Actor);
        }
    }
}
