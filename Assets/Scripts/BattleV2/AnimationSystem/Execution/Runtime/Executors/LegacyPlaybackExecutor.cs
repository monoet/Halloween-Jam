using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using BattleV2.AnimationSystem.Execution;
using BattleV2.AnimationSystem.Execution.Runtime;
using BattleV2.AnimationSystem.Execution.Routers;
using BattleV2.AnimationSystem.Timelines;
using BattleV2.AnimationSystem.Runtime;
using BattleV2.Core;
using UnityEngine;
using BattleV2.Orchestration.Runtime;
using BattleV2.Diagnostics;

namespace BattleV2.AnimationSystem.Execution.Runtime.Executors
{
    /// <summary>
    /// Temporary bridge executor: plays the legacy ActionTimeline path from within the StepScheduler pipeline
    /// and returns a trackable Task that completes when the sequencer releases its timeline lock.
    /// </summary>
    public sealed class LegacyPlaybackExecutor : IActionStepExecutor
    {
        public const string ExecutorId = "legacy_playback";

        private readonly ActionSequencerDriver sequencerDriver;
        private readonly TimelineRuntimeBuilder runtimeBuilder;
        private readonly AnimationClipResolver clipResolver;
        private readonly IMainThreadInvoker mainThreadInvoker;

        public LegacyPlaybackExecutor(
            ActionSequencerDriver sequencerDriver,
            TimelineRuntimeBuilder runtimeBuilder,
            AnimationClipResolver clipResolver,
            IMainThreadInvoker mainThreadInvoker)
        {
            this.sequencerDriver = sequencerDriver;
            this.runtimeBuilder = runtimeBuilder;
            this.clipResolver = clipResolver;
            this.mainThreadInvoker = mainThreadInvoker ?? MainThreadInvoker.Instance;
        }

        public string Id => ExecutorId;

        public bool CanExecute(ActionStep step) => true;

        public Task ExecuteAsync(StepExecutionContext context)
        {
            if (context.Actor == null || context.Timeline == null || runtimeBuilder == null || sequencerDriver == null)
            {
                return Task.CompletedTask;
            }

            if (BattleDebug.IsEnabled("EX"))
            {
                BattleDebug.Log("EX", 1, $"start legacy_playback actor={context.Actor.name} actionId={context.Timeline.ActionId ?? "(null)"}", context.Actor);
            }

            var wrapper = context.Wrapper;
            if (wrapper == null)
            {
                return Task.CompletedTask;
            }

            var cancellationToken = context.CancellationToken;
            return mainThreadInvoker.RunAsync(() =>
            {
                var session = new LegacyTimelineSession(
                    context.Request,
                    context.Timeline,
                    runtimeBuilder,
                    sequencerDriver,
                    wrapper,
                    clipResolver);

                return session.RunAsync(cancellationToken);
            });
        }

        private sealed class LegacyTimelineSession
        {
            private readonly AnimationRequest request;
            private readonly ActionTimeline timeline;
            private readonly TimelineRuntimeBuilder runtimeBuilder;
            private readonly ActionSequencerDriver driver;
            private readonly IAnimationWrapper wrapper;
            private readonly AnimationClipResolver clipResolver;

            private ActionSequencer sequencer;
            private TaskCompletionSource<bool> completion;
            private CancellationTokenRegistration cancellationRegistration;
            private CancellationTokenSource wrapperPlaybackCts;
            private Task wrapperPlaybackTask;
            private readonly string sequencerLockReason;
            private CancellationToken sessionToken;
            private readonly Stopwatch stopwatch = new Stopwatch();

            public LegacyTimelineSession(
                AnimationRequest request,
                ActionTimeline timeline,
                TimelineRuntimeBuilder runtimeBuilder,
                ActionSequencerDriver driver,
                IAnimationWrapper wrapper,
                AnimationClipResolver clipResolver)
            {
                this.request = request;
                this.timeline = timeline;
                this.runtimeBuilder = runtimeBuilder;
                this.driver = driver;
                this.wrapper = wrapper;
                this.clipResolver = clipResolver;

                completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                sequencerLockReason = string.IsNullOrWhiteSpace(timeline.ActionId)
                    ? "timeline"
                    : $"timeline:{timeline.ActionId}";
            }

            public Task RunAsync(CancellationToken cancellationToken)
            {
                if (request.Actor == null || timeline == null || runtimeBuilder == null || driver == null)
                {
                    return Task.CompletedTask;
                }

                stopwatch.Restart();
                sessionToken = cancellationToken;
                if (cancellationToken.CanBeCanceled)
                {
                    cancellationRegistration = cancellationToken.Register(OnCancelled);
                }

                sequencer = runtimeBuilder.Create(request, timeline);
                sequencer.EventDispatched += OnSequencerEvent;
                driver.Register(sequencer);
                return completion.Task;
            }

            private void OnCancelled()
            {
                CancelInternal(asCancellation: true);
            }

            private void OnSequencerEvent(SequencerEventInfo info)
            {
                if (completion.Task.IsCompleted)
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
                    if (BattleDebug.IsEnabled("EX"))
                    {
                        BattleDebug.Log("EX", 10, $"complete legacy_playback actionId={timeline.ActionId ?? "(null)"} ms={stopwatch.ElapsedMilliseconds}", request.Actor);
                    }
                    Cleanup();
                }
            }

            private void HandleAnimationPhase(in SequencerEventInfo info)
            {
                var payload = AnimationEventPayload.Parse(info.Payload);
                var clipId = payload.ResolveId("clip", "animation", "id");
                if (!clipResolver.TryGetClip(clipId, out var clip))
                {
                    return;
                }

                var playbackRequest = BuildPlaybackRequest(payload, clip);
                CancelWrapperPlayback();
                wrapperPlaybackCts = CancellationTokenSource.CreateLinkedTokenSource(sessionToken);
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

            private void CancelInternal(bool asCancellation)
            {
                if (completion.Task.IsCompleted)
                {
                    return;
                }

                try
                {
                    sequencer?.Cancel();
                }
                catch (Exception)
                {
                    // swallow
                }

                CancelWrapperPlayback();
                wrapper?.Stop();

                if (asCancellation)
                {
                    if (BattleDebug.IsEnabled("EX"))
                    {
                        BattleDebug.Warn("EX", 900, $"cancel legacy_playback actionId={timeline.ActionId ?? "(null)"} ms={stopwatch.ElapsedMilliseconds}", request.Actor);
                    }
                    completion.TrySetCanceled();
                }
                else
                {
                    completion.TrySetResult(false);
                }

                Cleanup();
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

            private void Cleanup()
            {
                if (cancellationRegistration != default)
                {
                    cancellationRegistration.Dispose();
                    cancellationRegistration = default;
                }

                if (sequencer != null)
                {
                    sequencer.EventDispatched -= OnSequencerEvent;
                }
            }
        }
    }
}
