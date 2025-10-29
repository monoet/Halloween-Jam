using System;
using System.Collections.Generic;
using BattleV2.AnimationSystem.Timelines;

namespace BattleV2.AnimationSystem.Execution
{
    public interface ITimelineCompiler
    {
        CompiledTimeline Compile(ActionTimeline timeline, float timelineDuration);
    }

    public sealed class TimelineCompiler : ITimelineCompiler
    {
        public CompiledTimeline Compile(ActionTimeline timeline, float timelineDuration)
        {
            if (timeline == null)
            {
                throw new ArgumentNullException(nameof(timeline));
            }

            timelineDuration = Math.Max(0.1f, timelineDuration);

            var tracks = timeline.Tracks;
            if (tracks == null || tracks.Count == 0)
            {
                return CompiledTimeline.Empty;
            }

            var phases = new List<CompiledPhase>();
            for (int i = 0; i < tracks.Count; i++)
            {
                var track = tracks[i];
                if (track.Phases == null || track.Phases.Count == 0)
                {
                    continue;
                }

                foreach (var phase in track.Phases)
                {
                    float startNorm = Math.Clamp(phase.Start, 0f, 1f);
                    float endNorm = Math.Clamp(phase.End, startNorm, 1f);
                    float start = startNorm * timelineDuration;
                    float end = endNorm * timelineDuration;
                    phases.Add(new CompiledPhase(
                        track.Type,
                        start,
                        end,
                        startNorm,
                        endNorm,
                        phase.OnEnterEvent,
                        phase.OnExitEvent,
                        phase.Payload));
                }
            }

            phases.Sort((a, b) => a.Start.CompareTo(b.Start));
            return new CompiledTimeline(timeline.ActionId, timeline.DisplayName, timelineDuration, phases);
        }
    }

    public readonly struct CompiledTimeline
    {
        public static readonly CompiledTimeline Empty = new(string.Empty, string.Empty, 0f, Array.Empty<CompiledPhase>());

        public CompiledTimeline(string actionId, string displayName, float duration, IReadOnlyList<CompiledPhase> phases)
        {
            ActionId = actionId;
            DisplayName = displayName;
            Duration = duration;
            Phases = phases ?? Array.Empty<CompiledPhase>();
        }

        public string ActionId { get; }
        public string DisplayName { get; }
        public float Duration { get; }
        public IReadOnlyList<CompiledPhase> Phases { get; }

        public bool IsEmpty => Duration <= 0f || Phases == null || Phases.Count == 0;
    }

    public readonly struct CompiledPhase
    {
        public CompiledPhase(
            ActionTimeline.TrackType trackType,
            float start,
            float end,
            float startNormalized,
            float endNormalized,
            string enterEvent,
            string exitEvent,
            string payload)
        {
            Track = trackType;
            Start = start;
            End = end;
            StartNormalized = startNormalized;
            EndNormalized = endNormalized;
            EnterEvent = enterEvent;
            ExitEvent = exitEvent;
            Payload = payload;
        }

        public ActionTimeline.TrackType Track { get; }
        public float Start { get; }
        public float End { get; }
        public float StartNormalized { get; }
        public float EndNormalized { get; }
        public string EnterEvent { get; }
        public string ExitEvent { get; }
        public string Payload { get; }

        public override string ToString() =>
            $"{Track} [{Start:0.00}-{End:0.00}] enter={EnterEvent} exit={ExitEvent} payload={Payload}";
    }
}
