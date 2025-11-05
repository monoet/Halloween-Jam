using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BattleV2.AnimationSystem;
using BattleV2.AnimationSystem.Catalog;
using BattleV2.AnimationSystem.Execution;
using BattleV2.AnimationSystem.Execution.Routers;
using BattleV2.AnimationSystem.Execution.Runtime;
using BattleV2.AnimationSystem.Runtime.Internal;
using BattleV2.AnimationSystem.Timelines;
using BattleV2.Core;
using UnityEngine;
using BattleV2.Orchestration.Runtime;

namespace BattleV2.AnimationSystem.Runtime
{
    /// <summary>
    /// Adapter defined in JRPG Animation System LOCKED (secciÃ³n 5).
    /// Coordinates timeline playback (sequencer + wrapper + routers) without touching BattleManagerV2.
    /// </summary>
    public sealed class NewAnimOrchestratorAdapter : IAnimationOrchestrator, IDisposable
    {
        private readonly TimelineRuntimeBuilder runtimeBuilder;
        private readonly ActionSequencerDriver sequencerDriver;
        private readonly ActionTimelineCatalog timelineCatalog;
        private readonly AnimatorWrapperResolver wrapperResolver;
        private readonly AnimationClipResolver clipResolver;
        private readonly AnimationRouterBundle routerBundle;
        private readonly StepScheduler stepScheduler;
        private readonly IAnimationEventBus eventBus;
        private readonly ITimedHitService timedHitService;
        private readonly AnimatorRegistry registry;
        private readonly Dictionary<CombatantState, AnimationSequenceSession> activeSessions = new();
        private readonly Dictionary<CombatantState, IAnimationWrapper> legacyAdapters = new();

        private bool disposed;

        public NewAnimOrchestratorAdapter(
            TimelineRuntimeBuilder runtimeBuilder,
            ActionSequencerDriver sequencerDriver,
            ActionTimelineCatalog timelineCatalog,
            IActionLockManager lockManager,
            IAnimationEventBus eventBus,
            ITimedHitService timedHitService,
            AnimatorWrapperResolver wrapperResolver,
            AnimationClipResolver clipResolver,
            AnimationRouterBundle routerBundle,
            StepScheduler stepScheduler,
            AnimatorRegistry registry)
        {
            this.runtimeBuilder = runtimeBuilder ?? throw new ArgumentNullException(nameof(runtimeBuilder));
            this.sequencerDriver = sequencerDriver ?? throw new ArgumentNullException(nameof(sequencerDriver));
            this.timelineCatalog = timelineCatalog ?? throw new ArgumentNullException(nameof(timelineCatalog));
            _ = lockManager ?? throw new ArgumentNullException(nameof(lockManager));
            this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            this.timedHitService = timedHitService ?? throw new ArgumentNullException(nameof(timedHitService));
            this.wrapperResolver = wrapperResolver ?? throw new ArgumentNullException(nameof(wrapperResolver));
            this.clipResolver = clipResolver ?? throw new ArgumentNullException(nameof(clipResolver));
            this.routerBundle = routerBundle ?? throw new ArgumentNullException(nameof(routerBundle));
            this.stepScheduler = stepScheduler ?? throw new ArgumentNullException(nameof(stepScheduler));
            this.registry = registry ?? AnimatorRegistry.Instance;
        }

        public async Task PlayAsync(AnimationRequest request, CancellationToken cancellationToken = default)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(NewAnimOrchestratorAdapter));
            }

            if (request.Actor == null)
            {
                BattleLogger.Warn("AnimAdapter", "AnimationRequest missing actor. Ignoring playback.");
                return;
            }

            var selection = request.Selection;
            var action = selection.Action;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[AnimAdapter] PlayAsync request for action '{action?.id ?? "(null)"}' (actor={request.Actor.name})");
#endif

            if (timelineCatalog == null || action == null)
            {
                BattleLogger.Warn("AnimAdapter", $"Missing catalog or action for request '{action?.id ?? "(null)"}'.");
                return;
            }

            var timeline = timelineCatalog.GetTimelineOrDefault(action.id);
            if (timeline == null)
            {
                BattleLogger.Warn("AnimAdapter", $"No timeline registered for action '{action.id}'.");
                return;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[AnimAdapter] Timeline '{timeline.ActionId}' resolved for action '{action.id}'.");
#endif

            IAnimationWrapper wrapper = null;
            if (registry != null)
            {
                registry.TryGetWrapper(request.Actor, out wrapper);
            }

            if (wrapper == null && wrapperResolver != null)
            {
                var legacyWrapper = wrapperResolver.Resolve(request.Actor);
                if (legacyWrapper != null)
                {
                    if (!legacyAdapters.TryGetValue(request.Actor, out wrapper))
                    {
                        wrapper = registry.ResolveLegacyWrapper(request.Actor, legacyWrapper);
                        if (wrapper != null)
                        {
                            legacyAdapters[request.Actor] = wrapper;
                        }
                    }
                }
            }

            if (wrapper == null)
            {
                BattleLogger.Warn("AnimAdapter", $"No AnimatorWrapper binding configured for actor '{request.Actor.name}'.");
                return;
            }

            if (activeSessions.TryGetValue(request.Actor, out var previousSession))
            {
                try
                {
                    await previousSession.CancelAsync();
                }
                catch (OperationCanceledException)
                {
                    // Expected when the previous session completes via cancellation.
                }

                if (activeSessions.TryGetValue(request.Actor, out var current) && ReferenceEquals(current, previousSession))
                {
                    activeSessions.Remove(request.Actor);
                }

                if (!previousSession.IsDisposed)
                {
                    previousSession.Dispose();
                }
            }

            var sequencer = runtimeBuilder.Create(request, timeline);
            var session = new AnimationSequenceSession(
                request,
                timeline,
                sequencer,
                wrapper,
                clipResolver,
                routerBundle,
                stepScheduler,
                eventBus,
                timedHitService);

            activeSessions[request.Actor] = session;
            try
            {
                await session.RunAsync(sequencerDriver, cancellationToken);
            }
            finally
            {
                if (activeSessions.TryGetValue(request.Actor, out var current) && ReferenceEquals(current, session))
                {
                    activeSessions.Remove(request.Actor);
                }

                session.Dispose();
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            routerBundle.Dispose();
            wrapperResolver.Dispose();
            legacyAdapters.Clear();
        }
    }
}
