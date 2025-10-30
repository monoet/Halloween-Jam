using System;
using System.Collections.Generic;
using BattleV2.AnimationSystem;
using BattleV2.Core;

namespace BattleV2.AnimationSystem.Execution.Routers
{
    /// <summary>
    /// Routes camera shake/zoom payloads emitted by the sequencer.
    /// </summary>
    public sealed class AnimationCameraRouter : IDisposable
    {
        private const string LogScope = "AnimCamera";

        private readonly IAnimationEventBus eventBus;
        private readonly IAnimationCameraService cameraService;
        private readonly List<IDisposable> subscriptions = new();
        private readonly HashSet<CombatantState> activeActors = new();

        private bool disposed;

        public AnimationCameraRouter(IAnimationEventBus eventBus, IAnimationCameraService cameraService)
        {
            this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            this.cameraService = cameraService ?? throw new ArgumentNullException(nameof(cameraService));

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
            activeActors.Clear();
        }

        private void OnImpact(AnimationImpactEvent evt)
        {
            HandlePayload(evt.Actor, evt.Payload, evt.Tag, "impact", evt, null);
        }

        private void OnPhase(AnimationPhaseEvent evt)
        {
            HandlePayload(evt.Actor, evt.Payload, evt.Payload, "phase", null, evt);
        }

        private void HandlePayload(
            CombatantState actor,
            string rawPayload,
            string tag,
            string source,
            AnimationImpactEvent? impact,
            AnimationPhaseEvent? phase)
        {
            if (disposed || string.IsNullOrWhiteSpace(rawPayload))
            {
                return;
            }

            var payload = AnimationEventPayload.Parse(rawPayload);
            string effectId = payload.ResolveId("camera", "shake", "effect", "id");
            if (string.IsNullOrWhiteSpace(effectId))
            {
                BattleLogger.Warn(LogScope, $"Missing camera effect id in {source} payload (tag={tag ?? "(null)"}). Payload='{payload}'.");
                return;
            }

            bool applied = cameraService.TryApply(effectId, actor, impact, phase, payload);
            if (!applied)
            {
                BattleLogger.Warn(LogScope, $"Camera effect '{effectId}' was not applied (actor={actor?.name ?? "(null)"}). Check bindings.");
                return;
            }

            if (actor != null)
            {
                activeActors.Add(actor);
            }
        }

        private void OnLock(AnimationLockEvent evt)
        {
            if (evt.Actor == null || evt.IsLocked || !activeActors.Contains(evt.Actor))
            {
                return;
            }

            cameraService.Reset(evt.Actor);
            activeActors.Remove(evt.Actor);
        }
    }
}
