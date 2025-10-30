using System;
using System.Collections.Generic;
using BattleV2.AnimationSystem;
using BattleV2.Core;

namespace BattleV2.AnimationSystem.Execution.Routers
{
    /// <summary>
    /// Bridges animation events to UI feedback (banners, prompts, etc.).
    /// </summary>
    public sealed class AnimationUiRouter : IDisposable
    {
        private const string LogScope = "AnimUI";

        private readonly IAnimationEventBus eventBus;
        private readonly IAnimationUiService uiService;
        private readonly List<IDisposable> subscriptions = new();
        private readonly HashSet<CombatantState> activeActors = new();

        private bool disposed;

        public AnimationUiRouter(IAnimationEventBus eventBus, IAnimationUiService uiService)
        {
            this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            this.uiService = uiService ?? throw new ArgumentNullException(nameof(uiService));

            subscriptions.Add(this.eventBus.Subscribe<AnimationPhaseEvent>(OnPhase));
            subscriptions.Add(this.eventBus.Subscribe<AnimationWindowEvent>(OnWindow));
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

        private void OnPhase(AnimationPhaseEvent evt)
        {
            Dispatch(evt.Actor, evt.Payload, evt, null, null, "phase");
        }

        private void OnWindow(AnimationWindowEvent evt)
        {
            // Only notify on open. Closures should be handled by Clear or a dedicated payload.
            if (!evt.IsOpening)
            {
                return;
            }

            string payload = string.IsNullOrWhiteSpace(evt.Payload) ? evt.Tag : evt.Payload;
            Dispatch(evt.Actor, payload, null, evt, null, "window");
        }

        private void Dispatch(
            CombatantState actor,
            string rawPayload,
            AnimationPhaseEvent? phase,
            AnimationWindowEvent? window,
            AnimationImpactEvent? impact,
            string source)
        {
            if (disposed || string.IsNullOrWhiteSpace(rawPayload))
            {
                return;
            }

            var payload = AnimationEventPayload.Parse(rawPayload);
            string uiId = payload.ResolveId("ui", "widget", "panel", "id");
            if (string.IsNullOrWhiteSpace(uiId))
            {
                BattleLogger.Warn(LogScope, $"Missing UI id in {source} payload (actor={actor?.name ?? "(null)"}). Payload='{payload}'.");
                return;
            }

            bool handled = uiService.TryHandle(uiId, actor, phase, window, impact, payload);
            if (!handled)
            {
                BattleLogger.Warn(LogScope, $"UI payload '{uiId}' was not handled (actor={actor?.name ?? "(null)"}). Verify bindings.");
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

            uiService.Clear(evt.Actor);
            activeActors.Remove(evt.Actor);
        }
    }
}
