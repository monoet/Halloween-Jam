using System;
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
            public int ErrorCount => Errors?.Count ?? 0;
            public int WarningCount => Warnings?.Count ?? 0;

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

            bool hasAnimationTrack = false;
            var globalTags = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < tracks.Count; i++)
            {
                var track = tracks[i];
                if (track.Phases == null || track.Phases.Count == 0)
                {
                    result.AddWarning($"Track {track.Type} en '{timeline.ActionId}' esta vacio.");
                    continue;
                }

                if (track.Type == ActionTimeline.TrackType.Animation && track.Phases.Count > 0)
                {
                    hasAnimationTrack = true;
                }

                var sortedPhases = new List<ActionTimeline.Phase>(track.Phases);
                sortedPhases.Sort((a, b) => a.Start.CompareTo(b.Start));

                for (int p = 0; p < sortedPhases.Count; p++)
                {
                    var phase = sortedPhases[p];
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

                    if (p > 0)
                    {
                        var previous = sortedPhases[p - 1];
                        if (phase.Start < previous.End)
                        {
                            result.AddWarning($"Track {track.Type} presenta solapamiento entre fases que inician en {previous.Start:0.00}-{previous.End:0.00} y {phase.Start:0.00}-{phase.End:0.00}.");
                        }
                    }

                    CheckTag(globalTags, phase.OnEnterEvent, timeline.ActionId, ref result);
                    CheckTag(globalTags, phase.OnExitEvent, timeline.ActionId, ref result);
                }
            }

            if (!hasAnimationTrack)
            {
                result.AddWarning($"Timeline '{timeline.ActionId}' no define un track de Animation; verifica que el actor tenga una pose base.");
            }

            if (timeline.Info.Tags != null && timeline.Info.Tags.Length > 0)
            {
                var metaTags = new HashSet<string>(StringComparer.Ordinal);
                for (int t = 0; t < timeline.Info.Tags.Length; t++)
                {
                    var tag = timeline.Info.Tags[t];
                    if (string.IsNullOrWhiteSpace(tag))
                    {
                        result.AddWarning($"Timeline '{timeline.ActionId}' tiene una etiqueta vacia (indice {t}).");
                        continue;
                    }

                    if (!metaTags.Add(tag))
                    {
                        result.AddWarning($"Timeline '{timeline.ActionId}' repite la etiqueta '{tag}'.");
                    }
                }
            }

            return result;
        }

        private static void CheckTag(HashSet<string> globalTags, string tag, string actionId, ref Result result)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return;
            }

            if (!globalTags.Add(tag))
            {
                result.AddWarning($"Timeline '{actionId}' reutiliza el tag '{tag}'. Asegura unicidad para simplificar los routers.");
            }
        }
    }
}
