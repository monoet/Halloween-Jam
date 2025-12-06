using System;
using System.Collections.Generic;
using BattleV2.Core;

namespace BattleV2.Marks
{
    /// <summary>
    /// Maintains marks per combatant keyed by mark id and raises events on changes.
    /// No gameplay logic beyond storage/notifications.
    /// </summary>
    public sealed class MarkService
    {
        private readonly Dictionary<CombatantState, Dictionary<string, MarkState>> marks = new();

        public event Action<MarkEvent> OnMarkChanged;

        public bool ApplyMark(CombatantState target, MarkDefinition definition)
        {
            if (target == null || definition == null)
            {
                return false;
            }

            var key = ResolveKey(definition);
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            var bucket = EnsureBucket(target);
            var reason = bucket.ContainsKey(key) ? MarkChangeReason.Refreshed : MarkChangeReason.Applied;
            bucket[key] = new MarkState(definition);
            RaiseEvent(target, definition, reason);
            return true;
        }

        public bool ClearMark(CombatantState target, string markId = null)
        {
            if (target == null)
            {
                return false;
            }

            if (!marks.TryGetValue(target, out var bucket) || bucket == null || bucket.Count == 0)
            {
                return false;
            }

            if (string.IsNullOrEmpty(markId))
            {
                var removedAny = bucket.Count > 0;
                if (removedAny)
                {
                    foreach (var kvp in bucket)
                    {
                        RaiseEvent(target, kvp.Value.Definition, MarkChangeReason.Cleared);
                    }
                    bucket.Clear();
                }
                return removedAny;
            }

            if (bucket.Remove(markId, out var state))
            {
                RaiseEvent(target, state.Definition, MarkChangeReason.Cleared);
                return true;
            }

            return false;
        }

        public bool DetonateMark(CombatantState target, string markId, CombatantState detonator = null, string reactionId = null)
        {
            if (target == null || string.IsNullOrEmpty(markId))
            {
                return false;
            }

            if (marks.TryGetValue(target, out var bucket) && bucket != null && bucket.Remove(markId, out var state))
            {
                RaiseEvent(target, state.Definition, MarkChangeReason.Detonated, detonator, reactionId);
                return true;
            }

            return false;
        }

        public bool HasMark(CombatantState target) =>
            target != null && marks.TryGetValue(target, out var bucket) && bucket != null && bucket.Count > 0;

        public bool HasMark(CombatantState target, string markId)
        {
            if (target == null || string.IsNullOrEmpty(markId))
            {
                return false;
            }

            return marks.TryGetValue(target, out var bucket) && bucket != null && bucket.ContainsKey(markId);
        }

        public bool TryGetMark(CombatantState target, string markId, out MarkDefinition definition)
        {
            definition = null;
            if (target == null || string.IsNullOrEmpty(markId))
            {
                return false;
            }

            if (marks.TryGetValue(target, out var bucket) && bucket != null && bucket.TryGetValue(markId, out var state))
            {
                definition = state.Definition;
                return true;
            }

            return false;
        }

        public IReadOnlyList<MarkDefinition> GetMarks(CombatantState target)
        {
            if (target == null)
            {
                return System.Array.Empty<MarkDefinition>();
            }

            if (!marks.TryGetValue(target, out var bucket) || bucket == null || bucket.Count == 0)
            {
                return System.Array.Empty<MarkDefinition>();
            }

            var keys = new List<string>(bucket.Keys);
            keys.Sort(System.StringComparer.Ordinal);

            var list = new List<MarkDefinition>(keys.Count);
            for (int i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                if (bucket.TryGetValue(key, out var state) && state.Definition != null)
                {
                    list.Add(state.Definition);
                }
            }

            return list;
        }

        private Dictionary<string, MarkState> EnsureBucket(CombatantState target)
        {
            if (!marks.TryGetValue(target, out var bucket) || bucket == null)
            {
                bucket = new Dictionary<string, MarkState>();
                marks[target] = bucket;
            }

            return bucket;
        }

        private static string ResolveKey(MarkDefinition definition)
        {
            if (definition == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(definition.id))
            {
                return definition.id;
            }

            return definition.name;
        }

        private readonly struct MarkState
        {
            public MarkState(MarkDefinition definition)
            {
                Definition = definition;
            }

            public MarkDefinition Definition { get; }
        }

        private void RaiseEvent(CombatantState target, MarkDefinition def, MarkChangeReason reason, CombatantState detonator = null, string reactionId = null)
        {
            if (OnMarkChanged == null)
            {
                return;
            }

            string markId = ResolveKey(def);
            var evt = new MarkEvent(
                target,
                markId,
                def,
                reason,
                detonator,
                reactionId);
            OnMarkChanged.Invoke(evt);
        }
    }

    public enum MarkChangeReason
    {
        Applied,
        Refreshed,
        Overwritten,
        Detonated,
        Expired,
        Cleared
    }

    public readonly struct MarkEvent
    {
        public MarkEvent(
            CombatantState target,
            string markId,
            MarkDefinition definition,
            MarkChangeReason reason,
            CombatantState detonator,
            string reactionId)
        {
            Target = target;
            MarkId = markId;
            Definition = definition;
            Reason = reason;
            Detonator = detonator;
            ReactionId = reactionId;
        }

        public CombatantState Target { get; }
        public string MarkId { get; }
        public MarkDefinition Definition { get; }
        public MarkChangeReason Reason { get; }
        public CombatantState Detonator { get; }
        public string ReactionId { get; }
    }
}
