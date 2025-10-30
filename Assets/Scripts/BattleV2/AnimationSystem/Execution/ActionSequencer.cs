using System;
using System.Collections.Generic;
using BattleV2.AnimationSystem.Timelines;
using BattleV2.Core;
using BattleV2.Providers;

namespace BattleV2.AnimationSystem.Execution
{
    public sealed class ActionSequencer : IDisposable
    {
        public const double DefaultEventTolerance = 0.0001d;

        private readonly AnimationRequest request;
        private readonly CompiledTimeline timeline;
        private readonly ICombatClock clock;
        private readonly IAnimationEventBus eventBus;
        private readonly IActionLockManager lockManager;
        private readonly List<SequencerScheduledEvent> schedule;
        private readonly string baseLockReason;
        private readonly double tolerance;
        private readonly ActionSequencerEventDispatcher dispatcher;

        private int nextEvent;
        private double startTime;
        private bool started;
        private bool completed;
        private bool cancelled;

        public event Action<SequencerEventInfo> EventDispatched;

        public ActionSequencer(
            AnimationRequest request,
            CompiledTimeline timeline,
            ICombatClock clock,
            IAnimationEventBus eventBus,
            IActionLockManager lockManager)
            : this(request, timeline, clock, eventBus, lockManager, DefaultEventTolerance)
        {
        }

        public ActionSequencer(
            AnimationRequest request,
            CompiledTimeline timeline,
            ICombatClock clock,
            IAnimationEventBus eventBus,
            IActionLockManager lockManager,
            double tolerance)
        {
            this.request = request;
            this.timeline = timeline;
            this.clock = clock;
            this.eventBus = eventBus;
            this.lockManager = lockManager;
            this.tolerance = Math.Max(1e-6d, tolerance);
            dispatcher = new ActionSequencerEventDispatcher(request, eventBus);

            baseLockReason = string.IsNullOrWhiteSpace(timeline.ActionId)
                ? "timeline"
                : $"timeline:{timeline.ActionId}";

            schedule = TimelineScheduleBuilder.Build(timeline);
        }

        public bool IsStarted => started;
        public bool IsCompleted => completed;
        public bool IsCancelled => cancelled;
        public CompiledTimeline Timeline => timeline;

        public void Start()
        {
            if (started)
            {
                return;
            }

            started = true;
            clock?.Sample();
            startTime = clock?.Now ?? 0d;

            lockManager?.PushLock(baseLockReason);
            PublishLockEvent(true, baseLockReason);
        }

        public void Tick()
        {
            if (!started || completed || cancelled)
            {
                return;
            }

            clock?.Sample();
            double now = clock?.Now ?? 0d;
            double elapsed = now - startTime;
            if (elapsed < 0d)
            {
                elapsed = 0d;
            }

            while (nextEvent < schedule.Count)
            {
                var current = schedule[nextEvent];
                if (current.TriggerTime - elapsed > tolerance)
                {
                    break;
                }

                ExecuteEvent(current, elapsed);
                nextEvent++;
            }

            if (nextEvent >= schedule.Count)
            {
                CompleteInternal();
            }
        }

        public void Cancel()
        {
            if (cancelled || completed)
            {
                return;
            }

            cancelled = true;
            PublishLockEvent(false, $"{baseLockReason}:cancelled");
            CompleteInternal();
        }

        public void Dispose()
        {
            if (!completed && !cancelled)
            {
                Cancel();
            }
        }

        private void CompleteInternal()
        {
            if (completed)
            {
                return;
            }

            completed = true;
            lockManager?.PopLock(baseLockReason);
            PublishLockEvent(false, baseLockReason);
        }

        private void ExecuteEvent(SequencerScheduledEvent scheduled, double elapsed)
        {
            EventDispatched?.Invoke(new SequencerEventInfo(
                scheduled.Type,
                scheduled.TriggerTime,
                elapsed,
                scheduled.Phase,
                scheduled.Index + 1,
                scheduled.TotalCount,
                scheduled.Tag,
                scheduled.Payload,
                scheduled.Reason));

            switch (scheduled.Type)
            {
                case ScheduledEventType.LockAcquire:
                    lockManager?.PushLock(scheduled.Reason);
                    PublishLockEvent(true, scheduled.Reason);
                    break;

                case ScheduledEventType.LockRelease:
                    lockManager?.PopLock(scheduled.Reason);
                    PublishLockEvent(false, scheduled.Reason);
                    break;

                case ScheduledEventType.WindowOpen:
                    dispatcher.PublishWindowEvent(scheduled, true);
                    break;

                case ScheduledEventType.WindowClose:
                    dispatcher.PublishWindowEvent(scheduled, false);
                    break;

                case ScheduledEventType.Impact:
                    dispatcher.PublishImpactEvent(scheduled);
                    break;

                case ScheduledEventType.PhaseEnter:
                    dispatcher.PublishPhaseEvent(scheduled);
                    break;
            }
        }

        private void PublishLockEvent(bool locked, string reason)
        {
            dispatcher.PublishLockEvent(locked, reason);
        }
    }

    public enum ScheduledEventType
    {
        WindowOpen,
        WindowClose,
        Impact,
        LockAcquire,
        LockRelease,
        PhaseEnter
    }

    public readonly struct SequencerEventInfo
    {
        public SequencerEventInfo(
            ScheduledEventType type,
            double scheduledTime,
            double elapsedTime,
            CompiledPhase phase,
            int index,
            int totalCount,
            string tag,
            string payload,
            string reason)
        {
            Type = type;
            ScheduledTime = scheduledTime;
            ElapsedTime = elapsedTime;
            Phase = phase;
            Index = index;
            TotalCount = totalCount;
            Tag = tag;
            Payload = payload;
            Reason = reason;
        }

        public ScheduledEventType Type { get; }
        public double ScheduledTime { get; }
        public double ElapsedTime { get; }
        public CompiledPhase Phase { get; }
        public int Index { get; }
        public int TotalCount { get; }
        public string Tag { get; }
        public string Payload { get; }
        public string Reason { get; }
    }
}
