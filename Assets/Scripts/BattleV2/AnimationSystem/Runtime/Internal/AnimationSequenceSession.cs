using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BattleV2.AnimationSystem;
using BattleV2.AnimationSystem.Execution;
using BattleV2.AnimationSystem.Execution.Routers;
using BattleV2.AnimationSystem.Execution.Runtime;
using BattleV2.AnimationSystem.Execution.Runtime.Recipes;
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
        private readonly StepScheduler stepScheduler;
        private readonly ActionRecipeCatalog recipeCatalog;
        private readonly IAnimationEventBus eventBus;
        private readonly ITimedHitService timedHitService;

        private readonly TaskCompletionSource<bool> completion;
        private CancellationTokenRegistration cancellationRegistration;
        private bool disposed;
        private readonly string sequencerLockReason;
        private CancellationToken sessionCancellationToken;
        private CancellationTokenSource wrapperPlaybackCts;
        private Task wrapperPlaybackTask;
        private CancellationTokenSource schedulerCts;
        private Task schedulerTask;

        public bool IsDisposed => disposed;

        public AnimationSequenceSession(
            AnimationRequest request,
            ActionTimeline timeline,
            ActionSequencer sequencer,
            IAnimationWrapper wrapper,
            AnimationClipResolver clipResolver,
            AnimationRouterBundle routerBundle,
            StepScheduler stepScheduler,
            ActionRecipeCatalog recipeCatalog,
            IAnimationEventBus eventBus,
            ITimedHitService timedHitService)
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
            if (stepScheduler == null) throw new ArgumentNullException(nameof(stepScheduler));
            if (recipeCatalog == null) throw new ArgumentNullException(nameof(recipeCatalog));
            if (eventBus == null) throw new ArgumentNullException(nameof(eventBus));
            if (timedHitService == null) throw new ArgumentNullException(nameof(timedHitService));

            this.request = request;
            this.timeline = timeline;
            this.sequencer = sequencer;
            this.wrapper = wrapper;
            this.clipResolver = clipResolver;
            this.routerBundle = routerBundle;
            this.stepScheduler = stepScheduler;
            this.recipeCatalog = recipeCatalog;
            this.eventBus = eventBus;
            this.timedHitService = timedHitService;

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

            if (TryHandleRecipe(payload))
            {
                return;
            }

            var clipId = payload.ResolveId("clip", "animation", "id");
            if (!clipResolver.TryGetClip(clipId, out var clip))
            {
                BattleLogger.Warn("AnimAdapter", $"Clip '{clipId ?? "(null)"}' not found for action '{timeline.ActionId}'.");
                return;
            }

            var playbackRequest = BuildPlaybackRequest(payload, clip);
            CancelScheduler();
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
            CancelScheduler();
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
            CancelScheduler();
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

        private void CancelScheduler()
        {
            if (schedulerCts == null)
            {
                return;
            }

            if (!schedulerCts.IsCancellationRequested)
            {
                schedulerCts.Cancel();
            }

            schedulerCts.Dispose();
            schedulerCts = null;
            schedulerTask = null;
        }

        private bool TryHandleRecipe(AnimationEventPayload payload)
        {
            if (stepScheduler == null)
            {
                return false;
            }

            if (!TryBuildRecipe(payload, out var recipe))
            {
                return false;
            }

            if (recipe == null || recipe.IsEmpty)
            {
                return false;
            }

            CancelWrapperPlayback();
            CancelScheduler();

            schedulerCts = CancellationTokenSource.CreateLinkedTokenSource(sessionCancellationToken);
            var bindingResolver = wrapper as IAnimationBindingResolver;
            if (bindingResolver == null)
            {
                BattleLogger.Warn("AnimAdapter", $"Wrapper for actor '{request.Actor?.name ?? "(null)"}' does not implement binding resolver.");
                schedulerCts.Dispose();
                schedulerCts = null;
                return false;
            }

            var context = new StepSchedulerContext(request, timeline, wrapper, bindingResolver, routerBundle, eventBus, timedHitService);
            var localCts = schedulerCts;
            schedulerTask = stepScheduler.ExecuteAsync(recipe, context, localCts.Token);
            schedulerTask.ContinueWith(t =>
            {
                if (t.IsFaulted && t.Exception != null)
                {
                    BattleLogger.Error("AnimAdapter", $"Recipe '{recipe.Id}' execution failed: {t.Exception.GetBaseException()}");
                }

                localCts.Dispose();

                if (ReferenceEquals(schedulerTask, t))
                {
                    schedulerTask = null;
                    schedulerCts = null;
                }
            }, TaskContinuationOptions.ExecuteSynchronously);

            return true;
        }

        private bool TryBuildRecipe(AnimationEventPayload payload, out ActionRecipe recipe)
        {
            recipe = null;

            string recipeId = null;
            if (payload.TryGetString("recipeId", out var explicitRecipeId) && !string.IsNullOrWhiteSpace(explicitRecipeId))
            {
                recipeId = explicitRecipeId;
            }
            else if (payload.TryGetString("recipe", out var altRecipeId) && !string.IsNullOrWhiteSpace(altRecipeId))
            {
                recipeId = altRecipeId;
            }

            if (!string.IsNullOrWhiteSpace(recipeId) &&
                (recipeCatalog.TryGet(recipeId, out var catalogRecipe) || stepScheduler.TryGetRecipe(recipeId, out catalogRecipe)))
            {
                recipe = catalogRecipe;
                return true;
            }

            if (!payload.TryGetString("steps", out var stepsRaw) || string.IsNullOrWhiteSpace(stepsRaw))
            {
                return false;
            }

            var steps = new List<ActionStep>(ActionStepParser.ParseList(stepsRaw));

            if (steps.Count == 0)
            {
                return false;
            }

            var group = new ActionStepGroup(null, steps, StepGroupExecutionMode.Sequential);
            var inlineId = !string.IsNullOrWhiteSpace(recipeId) ? recipeId : timeline.ActionId ?? "(inline)";
            recipe = new ActionRecipe(inlineId, new[] { group });
            return true;
        }
    }
}
