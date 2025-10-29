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
        private readonly List<ScheduledEvent> schedule;
        private readonly string baseLockReason;
        private readonly double tolerance;

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

            baseLockReason = string.IsNullOrWhiteSpace(timeline.ActionId)
                ? "timeline"
                : $"timeline:{timeline.ActionId}";

            schedule = BuildSchedule(timeline);
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

        private void ExecuteEvent(ScheduledEvent scheduled, double elapsed)
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
                    PublishWindowEvent(scheduled, true);
                    break;

                case ScheduledEventType.WindowClose:
                    PublishWindowEvent(scheduled, false);
                    break;

                case ScheduledEventType.Impact:
                    PublishImpactEvent(scheduled);
                    break;

                case ScheduledEventType.PhaseEnter:
                    PublishPhaseEvent(scheduled);
                    break;
            }
        }

        private void PublishPhaseEvent(ScheduledEvent scheduled)
        {
            // Index is 1-based to align with designer-friendly telemetry.
            var evt = new AnimationPhaseEvent(
                request.Actor,
                request.Selection,
                scheduled.Index + 1,
                scheduled.TotalCount,
                scheduled.Payload);
            eventBus?.Publish(evt);
        }

        private void PublishImpactEvent(ScheduledEvent scheduled)
        {
            CombatantState target = null;
            if (request.Targets != null && request.Targets.Count > 0)
            {
                int clamped = Math.Min(scheduled.Index, request.Targets.Count - 1);
                target = request.Targets[clamped];
            }

            var evt = new AnimationImpactEvent(
                request.Actor,
                target,
                request.Selection.Action,
                scheduled.Index + 1,
                scheduled.TotalCount,
                scheduled.Tag,
                scheduled.Payload);

            eventBus?.Publish(evt);
        }

        private void PublishWindowEvent(ScheduledEvent scheduled, bool isOpening)
        {
            var evt = new AnimationWindowEvent(
                request.Actor,
                scheduled.Tag,
                scheduled.Phase.StartNormalized,
                scheduled.Phase.EndNormalized,
                isOpening,
                scheduled.Index + 1,
                scheduled.TotalCount);
            eventBus?.Publish(evt);
        }

        private void PublishLockEvent(bool locked, string reason)
        {
            var evt = new AnimationLockEvent(request.Actor, locked, reason);
            eventBus?.Publish(evt);
        }

        private static List<ScheduledEvent> BuildSchedule(CompiledTimeline timeline)
        {
            var events = new List<ScheduledEvent>();
            if (timeline.Phases == null)
            {
                return events;
            }

            int totalAnimation = CountPhases(timeline, ActionTimeline.TrackType.Animation);
            int totalImpacts = CountPhases(timeline, ActionTimeline.TrackType.Impact);
            int totalWindows = CountPhases(timeline, ActionTimeline.TrackType.Window);

            int animationIndex = 0;
            int impactIndex = 0;
            int windowIndex = 0;

            foreach (var phase in timeline.Phases)
            {
                string tag = ResolveTag(phase);
                switch (phase.Track)
                {
                    case ActionTimeline.TrackType.Animation:
                        events.Add(ScheduledEvent.ForPhase(phase.Start, phase, animationIndex, totalAnimation, tag, phase.Payload));
                        animationIndex++;
                        break;

                    case ActionTimeline.TrackType.Window:
                        events.Add(ScheduledEvent.ForWindow(phase.Start, ScheduledEventType.WindowOpen, phase, windowIndex, totalWindows, tag, phase.Payload));
                        events.Add(ScheduledEvent.ForWindow(phase.End, ScheduledEventType.WindowClose, phase, windowIndex, totalWindows, tag, phase.Payload));
                        windowIndex++;
                        break;

                    case ActionTimeline.TrackType.Impact:
                        events.Add(ScheduledEvent.ForImpact(phase.Start, phase, impactIndex, totalImpacts, tag, phase.Payload));
                        impactIndex++;
                        break;

                    case ActionTimeline.TrackType.Lock:
                        string reason = !string.IsNullOrWhiteSpace(phase.Payload)
                            ? phase.Payload
                            : (string.IsNullOrWhiteSpace(tag) ? timeline.ActionId : tag);
                        events.Add(ScheduledEvent.ForLock(phase.Start, ScheduledEventType.LockAcquire, phase, reason));
                        events.Add(ScheduledEvent.ForLock(phase.End, ScheduledEventType.LockRelease, phase, reason));
                        break;

                    case ActionTimeline.TrackType.Custom:
                        // Treat custom tracks as phases for now to keep telemetry.
                        events.Add(ScheduledEvent.ForPhase(phase.Start, phase, animationIndex, totalAnimation, tag, phase.Payload));
                        break;
                }
            }

            events.Sort(ScheduledEventComparer.Instance);
            return events;
        }

        private static int CountPhases(CompiledTimeline timeline, ActionTimeline.TrackType track)
        {
            int count = 0;
            if (timeline.Phases == null)
            {
                return count;
            }

            for (int i = 0; i < timeline.Phases.Count; i++)
            {
                if (timeline.Phases[i].Track == track)
                {
                    count++;
                }
            }
            return count;
        }

        private static string ResolveTag(CompiledPhase phase)
        {
            if (!string.IsNullOrWhiteSpace(phase.EnterEvent))
            {
                return phase.EnterEvent;
            }

            if (!string.IsNullOrWhiteSpace(phase.ExitEvent))
            {
                return phase.ExitEvent;
            }

            return phase.Payload;
        }

        private readonly struct ScheduledEvent
        {
            public ScheduledEvent(
                double triggerTime,
                ScheduledEventType type,
                CompiledPhase phase,
                int index,
                int totalCount,
                string tag,
                string payload,
                string reason)
            {
                TriggerTime = triggerTime;
                Type = type;
                Phase = phase;
                Index = index;
                TotalCount = totalCount;
                Tag = tag;
                Payload = payload;
                Reason = reason;
            }

            public double TriggerTime { get; }
            public ScheduledEventType Type { get; }
            public CompiledPhase Phase { get; }
            public int Index { get; }
            public int TotalCount { get; }
            public string Tag { get; }
            public string Payload { get; }
            public string Reason { get; }

            public static ScheduledEvent ForPhase(double time, CompiledPhase phase, int index, int total, string tag, string payload) =>
                new ScheduledEvent(time, ScheduledEventType.PhaseEnter, phase, index, Math.Max(1, total), tag, payload, null);

            public static ScheduledEvent ForWindow(double time, ScheduledEventType type, CompiledPhase phase, int index, int total, string tag, string payload) =>
                new ScheduledEvent(time, type, phase, index, Math.Max(1, total), tag, payload, null);

            public static ScheduledEvent ForImpact(double time, CompiledPhase phase, int index, int total, string tag, string payload) =>
                new ScheduledEvent(time, ScheduledEventType.Impact, phase, index, Math.Max(1, total), tag, payload, null);

            public static ScheduledEvent ForLock(double time, ScheduledEventType type, CompiledPhase phase, string reason) =>
                new ScheduledEvent(time, type, phase, 0, 0, null, null, reason);
        }

        private sealed class ScheduledEventComparer : IComparer<ScheduledEvent>
        {
            public static readonly ScheduledEventComparer Instance = new();

            public int Compare(ScheduledEvent x, ScheduledEvent y)
            {
                int timeComparison = x.TriggerTime.CompareTo(y.TriggerTime);
                if (timeComparison != 0)
                {
                    return timeComparison;
                }

                return GetPriority(x.Type).CompareTo(GetPriority(y.Type));
            }

            private static int GetPriority(ScheduledEventType type) =>
                type switch
                {
                    ScheduledEventType.LockAcquire => -10,
                    ScheduledEventType.WindowOpen => 0,
                    ScheduledEventType.Impact => 1,
                    ScheduledEventType.WindowClose => 2,
                    ScheduledEventType.LockRelease => 3,
                    ScheduledEventType.PhaseEnter => 4,
                    _ => 5
                };
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
