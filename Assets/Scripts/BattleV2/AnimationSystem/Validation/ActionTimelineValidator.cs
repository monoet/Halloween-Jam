using System.Collections.Generic;
using UnityEngine;
using BattleV2.AnimationSystem.Timelines;

namespace BattleV2.AnimationSystem.Validation
{
    public static class ActionTimelineValidator
    {
        public struct Result
        {
            public bool IsValid;
            public List<string> Errors;
            public List<string> Warnings;

            public void AddError(string message)
            {
                Errors ??= new List<string>();
                Errors.Add(message);
                IsValid = false;
            }

            public void AddWarning(string message)
            {
                Warnings ??= new List<string>();
                Warnings.Add(message);
            }
        }

        public static Result Validate(ActionTimeline timeline)
        {
            var result = new Result { IsValid = true };
            if (timeline == null)
            {
                result.AddError("Timeline reference es null.");
                return result;
            }

            if (string.IsNullOrWhiteSpace(timeline.ActionId))
            {
                result.AddError($"Timeline '{timeline.name}' necesita ActionId.");
            }

            float length = Mathf.Clamp01(timeline.Info.Length <= 0f ? 1f : timeline.Info.Length);
            var tracks = timeline.Tracks;
            if (tracks == null || tracks.Count == 0)
            {
                result.AddWarning($"Timeline '{timeline.ActionId}' no tiene tracks.");
                return result;
            }

            for (int i = 0; i < tracks.Count; i++)
            {
                var track = tracks[i];
                if (track.Phases == null || track.Phases.Count == 0)
                {
                    result.AddWarning($"Track {track.Type} en '{timeline.ActionId}' está vacío.");
                    continue;
                }

                for (int p = 0; p < track.Phases.Count; p++)
                {
                    var phase = track.Phases[p];
                    if (phase.Start < 0f || phase.Start > 1f)
                    {
                        result.AddError($"Track {track.Type} fase {p} start fuera de rango (0..1).");
                    }

                    if (phase.End < phase.Start)
                    {
                        result.AddError($"Track {track.Type} fase {p} end < start.");
                    }

                    if (phase.End > length)
                    {
                        result.AddWarning($"Track {track.Type} fase {p} excede Length ({length}).");
                    }
                }
            }

            return result;
        }
    }
}
