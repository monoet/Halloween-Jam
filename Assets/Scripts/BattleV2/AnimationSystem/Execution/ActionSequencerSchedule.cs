using System;
using System.Collections.Generic;
using BattleV2.AnimationSystem.Timelines;

namespace BattleV2.AnimationSystem.Execution
{
    internal static class TimelineScheduleBuilder
    {
        public static List<SequencerScheduledEvent> Build(CompiledTimeline timeline)
        {
            var events = new List<SequencerScheduledEvent>();
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
                        events.Add(SequencerScheduledEvent.ForPhase(phase.Start, phase, animationIndex, totalAnimation, tag, phase.Payload));
                        animationIndex++;
                        break;

                    case ActionTimeline.TrackType.Window:
                        events.Add(SequencerScheduledEvent.ForWindow(phase.Start, ScheduledEventType.WindowOpen, phase, windowIndex, totalWindows, tag, phase.Payload));
                        events.Add(SequencerScheduledEvent.ForWindow(phase.End, ScheduledEventType.WindowClose, phase, windowIndex, totalWindows, tag, phase.Payload));
                        windowIndex++;
                        break;

                    case ActionTimeline.TrackType.Impact:
                        events.Add(SequencerScheduledEvent.ForImpact(phase.Start, phase, impactIndex, totalImpacts, tag, phase.Payload));
                        impactIndex++;
                        break;

                    case ActionTimeline.TrackType.Lock:
                        string reason = !string.IsNullOrWhiteSpace(phase.Payload)
                            ? phase.Payload
                            : (string.IsNullOrWhiteSpace(tag) ? timeline.ActionId : tag);
                        events.Add(SequencerScheduledEvent.ForLock(phase.Start, ScheduledEventType.LockAcquire, phase, reason));
                        events.Add(SequencerScheduledEvent.ForLock(phase.End, ScheduledEventType.LockRelease, phase, reason));
                        break;

                    case ActionTimeline.TrackType.Custom:
                        events.Add(SequencerScheduledEvent.ForPhase(phase.Start, phase, animationIndex, totalAnimation, tag, phase.Payload));
                        break;
                }
            }

            events.Sort(SequencerScheduledEventComparer.Instance);
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
    }

    internal readonly struct SequencerScheduledEvent
    {
        public SequencerScheduledEvent(
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

        public static SequencerScheduledEvent ForPhase(double time, CompiledPhase phase, int index, int total, string tag, string payload) =>
            new SequencerScheduledEvent(time, ScheduledEventType.PhaseEnter, phase, index, Math.Max(1, total), tag, payload, null);

        public static SequencerScheduledEvent ForWindow(double time, ScheduledEventType type, CompiledPhase phase, int index, int total, string tag, string payload) =>
            new SequencerScheduledEvent(time, type, phase, index, Math.Max(1, total), tag, payload, null);

        public static SequencerScheduledEvent ForImpact(double time, CompiledPhase phase, int index, int total, string tag, string payload) =>
            new SequencerScheduledEvent(time, ScheduledEventType.Impact, phase, index, Math.Max(1, total), tag, payload, null);

        public static SequencerScheduledEvent ForLock(double time, ScheduledEventType type, CompiledPhase phase, string reason) =>
            new SequencerScheduledEvent(time, type, phase, 0, 0, null, null, reason);
    }

    internal sealed class SequencerScheduledEventComparer : IComparer<SequencerScheduledEvent>
    {
        public static readonly SequencerScheduledEventComparer Instance = new();

        public int Compare(SequencerScheduledEvent x, SequencerScheduledEvent y)
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

