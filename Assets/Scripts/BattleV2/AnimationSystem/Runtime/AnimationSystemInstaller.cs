using System;
using System.Threading;
using System.Threading.Tasks;
using BattleV2.AnimationSystem.Catalog;
using BattleV2.AnimationSystem.Execution;
using BattleV2.AnimationSystem.Execution.Runtime;
using BattleV2.AnimationSystem.Timelines;
using BattleV2.Core;
using UnityEngine;

namespace BattleV2.AnimationSystem.Runtime
{
    /// <summary>
    /// Scene-level composition root for the JRPG animation system.
    /// Instantiates core services and exposes an orchestrator implementation.
    /// </summary>
    public sealed class AnimationSystemInstaller : MonoBehaviour
    {
        public static AnimationSystemInstaller Current { get; private set; }

        [Header("Dependencies")]
        [SerializeField] private ActionSequencerDriver sequencerDriver;
        [SerializeField] private ActionTimelineCatalog timelineCatalog;

        [Header("Optional Services")]
        [SerializeField] private CombatClock combatClock;

        private AnimationEventBus eventBus;
        private ActionLockManager lockManager;
        private TimelineCompiler timelineCompiler;
        private TimelineRuntimeBuilder runtimeBuilder;
        private TimedInputBuffer timedInputBuffer;
        private DefaultTimedHitToleranceProfile toleranceProfile;
        private TimedHitService timedHitService;
        private AnimationOrchestrator orchestrator;

        public IAnimationEventBus EventBus => eventBus;
        public ICombatClock Clock => combatClock;
        public IActionLockManager LockManager => lockManager;
        public ITimedHitService TimedHitService => timedHitService;
        public IAnimationOrchestrator Orchestrator => orchestrator;

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Debug.LogWarning("[AnimationSystemInstaller] Multiple installers detected. Replacing the active instance.");
            }

            Current = this;

            combatClock ??= new CombatClock();
            eventBus = new AnimationEventBus();
            lockManager = new ActionLockManager();
            timelineCompiler = new TimelineCompiler();
            runtimeBuilder = new TimelineRuntimeBuilder(timelineCompiler, combatClock, eventBus, lockManager);
            timedInputBuffer = new TimedInputBuffer(combatClock);
            toleranceProfile = new DefaultTimedHitToleranceProfile();
            timedHitService = new TimedHitService(combatClock, timedInputBuffer, toleranceProfile, eventBus);
            orchestrator = new AnimationOrchestrator(runtimeBuilder, sequencerDriver, timelineCatalog, lockManager, eventBus);

            if (sequencerDriver == null)
            {
                Debug.LogError("[AnimationSystemInstaller] Missing ActionSequencerDriver reference.", this);
            }

            if (timelineCatalog == null)
            {
                Debug.LogError("[AnimationSystemInstaller] Missing ActionTimelineCatalog reference.", this);
            }
        }

        private void OnDestroy()
        {
            timedHitService?.Dispose();
            if (Current == this)
            {
                Current = null;
            }
        }

        private sealed class AnimationOrchestrator : IAnimationOrchestrator
        {
            private readonly TimelineRuntimeBuilder runtimeBuilder;
            private readonly ActionSequencerDriver sequencerDriver;
            private readonly ActionTimelineCatalog timelineCatalog;
            private readonly IActionLockManager lockManager;
            private readonly IAnimationEventBus eventBus;

            public AnimationOrchestrator(
                TimelineRuntimeBuilder runtimeBuilder,
                ActionSequencerDriver sequencerDriver,
                ActionTimelineCatalog timelineCatalog,
                IActionLockManager lockManager,
                IAnimationEventBus eventBus)
            {
                this.runtimeBuilder = runtimeBuilder ?? throw new ArgumentNullException(nameof(runtimeBuilder));
                this.sequencerDriver = sequencerDriver ?? throw new ArgumentNullException(nameof(sequencerDriver));
                this.timelineCatalog = timelineCatalog ?? throw new ArgumentNullException(nameof(timelineCatalog));
                this.lockManager = lockManager ?? throw new ArgumentNullException(nameof(lockManager));
                this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            }

            public Task PlayAsync(AnimationRequest request, CancellationToken cancellationToken = default)
            {
                ActionTimeline timeline = null;
                if (timelineCatalog != null && request.Selection.Action != null)
                {
                    timeline = timelineCatalog.GetTimelineOrDefault(request.Selection.Action.id);
                }

                if (timeline == null)
                {
                    Debug.LogWarning($"[AnimationOrchestrator] No timeline found for action '{request.Selection.Action?.id}'.", timelineCatalog);
                    return Task.CompletedTask;
                }

                var sequencer = runtimeBuilder.Create(request, timeline);
                var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                void Cleanup()
                {
                    sequencer.EventDispatched -= OnSequencerEvent;
                }

                void OnSequencerEvent(SequencerEventInfo info)
                {
                    if (info.Type == ScheduledEventType.LockRelease && info.Reason == $"timeline:{timeline.ActionId}")
                    {
                        Cleanup();
                        completion.TrySetResult(true);
                    }
                }

                sequencer.EventDispatched += OnSequencerEvent;
                sequencerDriver.Register(sequencer);

                if (cancellationToken.CanBeCanceled)
                {
                    cancellationToken.Register(() =>
                    {
                        sequencer.Cancel();
                        Cleanup();
                        completion.TrySetCanceled(cancellationToken);
                    });
                }

                completion.Task.ContinueWith(
                    _ => Cleanup(),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Current);

                return completion.Task;
            }
        }
    }
}
