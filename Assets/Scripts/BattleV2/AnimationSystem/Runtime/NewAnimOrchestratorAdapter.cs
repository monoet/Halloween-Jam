using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BattleV2.AnimationSystem.Catalog;
using BattleV2.AnimationSystem.Execution;
using BattleV2.AnimationSystem.Execution.Routers;
using BattleV2.AnimationSystem.Timelines;
using BattleV2.Core;
using UnityEngine;

namespace BattleV2.AnimationSystem.Runtime
{
    /// <summary>
    /// Adapter defined in JRPG Animation System LOCKED (sección 5).
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

        private bool disposed;

        public NewAnimOrchestratorAdapter(
            TimelineRuntimeBuilder runtimeBuilder,
            ActionSequencerDriver sequencerDriver,
            ActionTimelineCatalog timelineCatalog,
            IActionLockManager lockManager,
            IAnimationEventBus eventBus,
            AnimatorWrapperResolver wrapperResolver,
            AnimationClipResolver clipResolver,
            AnimationRouterBundle routerBundle)
        {
            this.runtimeBuilder = runtimeBuilder ?? throw new ArgumentNullException(nameof(runtimeBuilder));
            this.sequencerDriver = sequencerDriver ?? throw new ArgumentNullException(nameof(sequencerDriver));
            this.timelineCatalog = timelineCatalog ?? throw new ArgumentNullException(nameof(timelineCatalog));
            _ = lockManager ?? throw new ArgumentNullException(nameof(lockManager));
            _ = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            this.wrapperResolver = wrapperResolver ?? throw new ArgumentNullException(nameof(wrapperResolver));
            this.clipResolver = clipResolver ?? throw new ArgumentNullException(nameof(clipResolver));
            this.routerBundle = routerBundle ?? throw new ArgumentNullException(nameof(routerBundle));
        }

        public Task PlayAsync(AnimationRequest request, CancellationToken cancellationToken = default)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(NewAnimOrchestratorAdapter));
            }

            if (request.Actor == null)
            {
                BattleLogger.Warn("AnimAdapter", "AnimationRequest missing actor. Ignoring playback.");
                return Task.CompletedTask;
            }

            var selection = request.Selection;
            var action = selection.Action;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[AnimAdapter] PlayAsync request for action '{action?.id ?? "(null)"}' (actor={request.Actor.name})");
#endif

            if (timelineCatalog == null || action == null)
            {
                BattleLogger.Warn("AnimAdapter", $"Missing catalog or action for request '{action?.id ?? "(null)"}'.");
                return Task.CompletedTask;
            }

            var timeline = timelineCatalog.GetTimelineOrDefault(action.id);
            if (timeline == null)
            {
                BattleLogger.Warn("AnimAdapter", $"No timeline registered for action '{action.id}'.");
                return Task.CompletedTask;
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[AnimAdapter] Timeline '{timeline.ActionId}' resolved for action '{action.id}'.");
#endif

            var wrapper = wrapperResolver.Resolve(request.Actor);
            if (wrapper == null)
            {
                BattleLogger.Warn("AnimAdapter", $"No AnimatorWrapper binding configured for actor '{request.Actor.name}'.");
                return Task.CompletedTask;
            }

            var sequencer = runtimeBuilder.Create(request, timeline);
            var session = new AnimationSequenceSession(
                request,
                timeline,
                sequencer,
                wrapper,
                clipResolver,
                routerBundle);

            var completion = session.Start(sequencerDriver, cancellationToken);
            return completion;
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
        }

        private sealed class AnimationSequenceSession : IDisposable
        {
            private readonly AnimationRequest request;
            private readonly ActionTimeline timeline;
            private readonly ActionSequencer sequencer;
            private readonly AnimatorWrapper wrapper;
            private readonly AnimationClipResolver clipResolver;
            private readonly AnimationRouterBundle routerBundle;

            private readonly TaskCompletionSource<bool> completion;
            private CancellationTokenRegistration cancellationRegistration;
            private bool disposed;
            private readonly string sequencerLockReason;

            public AnimationSequenceSession(
                AnimationRequest request,
                ActionTimeline timeline,
                ActionSequencer sequencer,
                AnimatorWrapper wrapper,
                AnimationClipResolver clipResolver,
                AnimationRouterBundle routerBundle)
            {
                this.request = request;
                this.timeline = timeline;
                this.sequencer = sequencer;
                this.wrapper = wrapper;
                this.clipResolver = clipResolver;
                this.routerBundle = routerBundle;

                completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                sequencerLockReason = string.IsNullOrWhiteSpace(timeline.ActionId)
                    ? "timeline"
                    : $"timeline:{timeline.ActionId}";
            }

            public Task Start(ActionSequencerDriver driver, CancellationToken cancellationToken)
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(nameof(AnimationSequenceSession));
                }

                sequencer.EventDispatched += OnSequencerEvent;
                routerBundle.RegisterActor(request.Actor);
                driver.Register(sequencer);

                if (cancellationToken.CanBeCanceled)
                {
                    cancellationRegistration = cancellationToken.Register(OnCancelled);
                }

                completion.Task.ContinueWith(
                    _ => Dispose(),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Current);

                return completion.Task;
            }

            private void OnSequencerEvent(SequencerEventInfo info)
            {
                if (disposed)
                {
                    return;
                }

                if (info.Type == ScheduledEventType.PhaseEnter &&
                    info.Phase.Track == ActionTimeline.TrackType.Animation)
                {
                    HandleAnimationPhase(info);
                }

                if (info.Type == ScheduledEventType.LockRelease &&
                    string.Equals(info.Reason, sequencerLockReason, StringComparison.Ordinal))
                {
                    completion.TrySetResult(true);
                }
            }

            private void HandleAnimationPhase(in SequencerEventInfo info)
            {
                var payload = AnimationEventPayload.Parse(info.Payload);
                var clipId = payload.ResolveId("clip", "animation", "id");
                if (!clipResolver.TryGetClip(clipId, out var clip))
                {
                    BattleLogger.Warn("AnimAdapter", $"Clip '{clipId ?? "(null)"}' not found for action '{timeline.ActionId}'.");
                    return;
                }

                var options = BuildClipOptions(payload, info);
                wrapper.PlayClip(clip, options);
            }

            private static AnimatorClipOptions BuildClipOptions(AnimationEventPayload payload, SequencerEventInfo info)
            {
                float speed = 1f;
                if (payload.TryGetFloat("speed", out var speedValue))
                {
                    speed = Mathf.Approximately(speedValue, 0f) ? 1f : speedValue;
                }

                float normalizedStart = 0f;
                if (payload.TryGetFloat("start", out var startNorm))
                {
                    normalizedStart = Mathf.Clamp01(startNorm);
                }
                else if (payload.TryGetFloat("startNormalized", out var alt))
                {
                    normalizedStart = Mathf.Clamp01(alt);
                }

                bool loop = true;
                if (payload.TryGetBool("loop", out var loopValue))
                {
                    loop = loopValue;
                }

                return new AnimatorClipOptions(
                    loop,
                    normalizedStart,
                    speed,
                    applyFootIK: true,
                    applyPlayableIK: false,
                    overrideDuration: 0d);
            }

            private void OnCancelled()
            {
                if (disposed)
                {
                    return;
                }

                sequencer.Cancel();
                wrapper.Stop();
                completion.TrySetCanceled();
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                sequencer.EventDispatched -= OnSequencerEvent;
                cancellationRegistration.Dispose();
                routerBundle.UnregisterActor(request.Actor);
                wrapper.ResetToFallback(0.2f);
            }
        }
    }

    public sealed class AnimationRouterBundle : IDisposable
    {
        private readonly AnimationVfxRouter vfxRouter;
        private readonly AnimationSfxRouter sfxRouter;
        private readonly AnimationCameraRouter cameraRouter;
        private readonly AnimationUiRouter uiRouter;
        private readonly IAnimationVfxService vfxService;
        private readonly IAnimationSfxService sfxService;
        private readonly IAnimationCameraService cameraService;
        private readonly IAnimationUiService uiService;

        public AnimationRouterBundle(
            IAnimationEventBus eventBus,
            IAnimationVfxService vfxService,
            IAnimationSfxService sfxService,
            IAnimationCameraService cameraService,
            IAnimationUiService uiService)
        {
            this.vfxService = vfxService ?? new NullVfxService();
            this.sfxService = sfxService ?? new NullSfxService();
            this.cameraService = cameraService ?? new NullCameraService();
            this.uiService = uiService ?? new NullUiService();

            vfxRouter = new AnimationVfxRouter(eventBus, this.vfxService);
            sfxRouter = new AnimationSfxRouter(eventBus, this.sfxService);
            cameraRouter = new AnimationCameraRouter(eventBus, this.cameraService);
            uiRouter = new AnimationUiRouter(eventBus, this.uiService);
        }

        public void RegisterActor(CombatantState actor)
        {
            if (actor == null)
            {
                return;
            }

            // Gives services a chance to prepare state per actor.
            vfxService.StopAllFor(actor);
            sfxService.StopAllFor(actor);
            cameraService.Reset(actor);
            uiService.Clear(actor);
        }

        public void UnregisterActor(CombatantState actor)
        {
            if (actor == null)
            {
                return;
            }

            vfxService.StopAllFor(actor);
            sfxService.StopAllFor(actor);
            cameraService.Reset(actor);
            uiService.Clear(actor);
        }

        public void Dispose()
        {
            vfxRouter?.Dispose();
            sfxRouter?.Dispose();
            cameraRouter?.Dispose();
            uiRouter?.Dispose();
        }

        private sealed class NullVfxService : IAnimationVfxService
        {
            public bool TryPlay(string vfxId, in AnimationImpactEvent evt, in AnimationEventPayload payload)
            {
                return false;
            }

            public void StopAllFor(CombatantState actor) { }
        }

        private sealed class NullSfxService : IAnimationSfxService
        {
            public bool TryPlay(string sfxId, CombatantState actor, AnimationImpactEvent? impactEvent, AnimationPhaseEvent? phaseEvent, in AnimationEventPayload payload)
            {
                return false;
            }

            public void StopAllFor(CombatantState actor) { }
        }

        private sealed class NullCameraService : IAnimationCameraService
        {
            public bool TryApply(string effectId, CombatantState actor, AnimationImpactEvent? impactEvent, AnimationPhaseEvent? phaseEvent, in AnimationEventPayload payload)
            {
                return false;
            }

            public void Reset(CombatantState actor) { }
        }

        private sealed class NullUiService : IAnimationUiService
        {
            public bool TryHandle(string uiId, CombatantState actor, AnimationPhaseEvent? phaseEvent, AnimationWindowEvent? windowEvent, AnimationImpactEvent? impactEvent, in AnimationEventPayload payload)
            {
                return false;
            }

            public void Clear(CombatantState actor) { }
        }
    }

    public sealed class AnimatorWrapperResolver : IDisposable
    {
        private readonly Dictionary<CombatantState, AnimatorWrapper> wrappers = new();
        private readonly Dictionary<CombatantState, AnimatorWrapperBinding> bindings = new();

        public AnimatorWrapperResolver(IEnumerable<AnimationActorBinding> actorBindings)
        {
            if (actorBindings == null)
            {
                return;
            }

            foreach (var binding in actorBindings)
            {
                AddOrUpdateBinding(binding);
            }
        }

        public AnimatorWrapper Resolve(CombatantState actor)
        {
            if (actor == null)
            {
                return null;
            }

            if (wrappers.TryGetValue(actor, out var existing))
            {
                return existing;
            }

            if (!bindings.TryGetValue(actor, out var binding))
            {
                return null;
            }

            var wrapper = new AnimatorWrapper(binding);
            wrappers[actor] = wrapper;
            return wrapper;
        }

        public void Dispose()
        {
            foreach (var kvp in wrappers)
            {
                kvp.Value?.Dispose();
            }

            wrappers.Clear();
            bindings.Clear();
        }

        public void AddOrUpdateBinding(AnimationActorBinding binding)
        {
            if (binding == null || !binding.IsValid)
            {
                return;
            }

            var wrapperBinding = new AnimatorWrapperBinding(binding.Animator, binding.FallbackClip, binding.Sockets);
            bindings[binding.Actor] = wrapperBinding;

            if (wrappers.TryGetValue(binding.Actor, out var existing))
            {
                existing.Dispose();
                wrappers.Remove(binding.Actor);
            }
        }
    }
}
