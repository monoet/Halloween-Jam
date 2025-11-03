using System;
using System.Threading;
using System.Threading.Tasks;
using BattleV2.AnimationSystem.Execution;
using BattleV2.AnimationSystem.Execution.Routers;
using BattleV2.AnimationSystem.Timelines;
using BattleV2.Core;
using UnityEngine;
using BattleV2.Orchestration.Runtime;

namespace BattleV2.AnimationSystem.Runtime.Internal
{
    internal sealed class AnimationSequenceSession : IDisposable
    {
        private readonly AnimationRequest request;
        private readonly ActionTimeline timeline;
        private readonly ActionSequencer sequencer;
        private readonly IAnimationWrapper wrapper;
        private readonly AnimationClipResolver clipResolver;
        private readonly AnimationRouterBundle routerBundle;

        private readonly TaskCompletionSource<bool> completion;
        private CancellationTokenRegistration cancellationRegistration;
        private bool disposed;
        private readonly string sequencerLockReason;
        private CancellationToken sessionCancellationToken;
        private CancellationTokenSource wrapperPlaybackCts;
        private Task wrapperPlaybackTask;

        public bool IsDisposed => disposed;

        public AnimationSequenceSession(
            AnimationRequest request,
            ActionTimeline timeline,
            ActionSequencer sequencer,
            IAnimationWrapper wrapper,
            AnimationClipResolver clipResolver,
            AnimationRouterBundle routerBundle)
        {
            if (request.Actor == null)
            {
                throw new ArgumentException("AnimationRequest must have a valid actor.", nameof(request));
            }
            if (timeline == null) throw new ArgumentNullException(nameof(timeline));
            if (sequencer == null) throw new ArgumentNullException(nameof(sequencer));
            if (wrapper == null) throw new ArgumentNullException(nameof(wrapper));
            if (clipResolver == null) throw new ArgumentNullException(nameof(clipResolver));
            if (routerBundle == null) throw new ArgumentNullException(nameof(routerBundle));

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

        public Task RunAsync(ActionSequencerDriver driver, CancellationToken cancellationToken)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(AnimationSequenceSession));
            }

            sequencer.EventDispatched += OnSequencerEvent;
            routerBundle.RegisterActor(request.Actor);
            driver.Register(sequencer);
            sessionCancellationToken = cancellationToken;

            if (cancellationToken.CanBeCanceled)
            {
                cancellationRegistration = cancellationToken.Register(OnCancelled);
            }

            return completion.Task;
        }

        public Task CancelAsync()
        {
            CancelInternal(asCancellation: false);
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


            var playbackRequest = BuildPlaybackRequest(payload, clip);
            CancelWrapperPlayback();
            wrapperPlaybackCts = CancellationTokenSource.CreateLinkedTokenSource(sessionCancellationToken);
            wrapperPlaybackTask = wrapper.PlayAsync(playbackRequest, wrapperPlaybackCts.Token);
        }

        private static AnimationPlaybackRequest BuildPlaybackRequest(AnimationEventPayload payload, AnimationClip clip)
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

            return AnimationPlaybackRequest.ForAnimatorClip(clip, speed, normalizedStart, loop);
        }

        private void OnCancelled()
        {
            if (disposed)
            {
                return;
            }

            CancelInternal(asCancellation: true);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            sequencer.EventDispatched -= OnSequencerEvent;
            if (cancellationRegistration != default)
            {
                cancellationRegistration.Dispose();
            }

            routerBundle.UnregisterActor(request.Actor);
            CancelWrapperPlayback();
            wrapper.Stop();
        }

        private void CancelInternal(bool asCancellation)
        {
            if (disposed || completion.Task.IsCompleted)
            {
                return;
            }

            sequencer.Cancel();
            CancelWrapperPlayback();
            wrapper.Stop();

            if (asCancellation)
            {
                completion.TrySetCanceled();
            }
            else
            {
                completion.TrySetResult(false);
            }
        }

        private void CancelWrapperPlayback()
        {
            if (wrapperPlaybackCts == null)
            {
                return;
            }

            if (!wrapperPlaybackCts.IsCancellationRequested)
            {
                wrapperPlaybackCts.Cancel();
            }

            wrapperPlaybackCts.Dispose();
            wrapperPlaybackCts = null;
            wrapperPlaybackTask = null;
        }
    }
}
