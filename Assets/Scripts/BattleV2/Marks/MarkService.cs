using System;
using System.Collections.Generic;
using BattleV2.Core;
using UnityEngine;

namespace BattleV2.Marks
{
    /// <summary>
    /// Hub de eventos y helpers para marks. La fuente de verdad es el slot en CombatantState.
    /// </summary>
    public sealed class MarkService
    {
        public event Action<MarkEvent> OnMarkChanged;

        public bool ApplyMark(
            CombatantState target,
            MarkDefinition definition,
            CombatantState appliedBy = null,
            int appliedAtOwnerTurnCounter = 0,
            int remainingTurns = 1)
        {
            UnityThread.AssertMainThread("MarkService.ApplyMark");

            if (target == null || definition == null)
            {
                return false;
            }

            var key = ResolveKey(definition);
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            int duration = remainingTurns > 0 ? remainingTurns : (definition.baseDurationTurns > 0 ? definition.baseDurationTurns : 1);
            remainingTurns = Mathf.Max(1, duration);

            var current = target.ActiveMark;
            var reason = MarkChangeReason.Applied;

            if (current.HasValue)
            {
                if (string.Equals(current.MarkId, key, StringComparison.Ordinal))
                {
                    reason = MarkChangeReason.Refreshed;
                }
                else
                {
                    reason = MarkChangeReason.Overwritten;
                }
            }

            var slot = new MarkSlot(definition, appliedBy, appliedAtOwnerTurnCounter, remainingTurns);
            target.SetMarkSlot(slot);
            RaiseEvent(target, definition, reason, appliedBy, null, slot);
            return true;
        }

        public bool ClearMark(CombatantState target, string markId = null, MarkChangeReason reason = MarkChangeReason.Cleared)
        {
            UnityThread.AssertMainThread("MarkService.ClearMark");

            if (target == null)
            {
                return false;
            }

            var slot = target.ActiveMark;
            if (!slot.HasValue)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(markId) && !string.Equals(markId, slot.MarkId, StringComparison.Ordinal))
            {
                return false;
            }

            target.ClearMarkSlot();
            RaiseEvent(target, slot.Definition, reason, slot.AppliedBy, null, slot);
            return true;
        }

        public bool DetonateMark(CombatantState target, string markId, CombatantState detonator = null, string reactionId = null)
        {
            UnityThread.AssertMainThread("MarkService.DetonateMark");

            if (target == null)
            {
                return false;
            }

            var slot = target.ActiveMark;
            if (!slot.HasValue)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(markId) && !string.Equals(markId, slot.MarkId, StringComparison.Ordinal))
            {
                return false;
            }

            target.ClearMarkSlot();
            RaiseEvent(target, slot.Definition, MarkChangeReason.Detonated, detonator ?? slot.AppliedBy, reactionId, slot);
            return true;
        }

        public bool HasMark(CombatantState target)
        {
            if (target == null)
            {
                return false;
            }

            return target.ActiveMark.HasValue;
        }

        public bool HasMark(CombatantState target, string markId)
        {
            if (target == null || string.IsNullOrEmpty(markId))
            {
                return false;
            }

            var slot = target.ActiveMark;
            return slot.HasValue && string.Equals(slot.MarkId, markId, StringComparison.Ordinal);
        }

        public bool TryGetMark(CombatantState target, string markId, out MarkDefinition definition)
        {
            definition = null;
            if (target == null)
            {
                return false;
            }

            var slot = target.ActiveMark;
            if (!slot.HasValue)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(markId) && !string.Equals(markId, slot.MarkId, StringComparison.Ordinal))
            {
                return false;
            }

            definition = slot.Definition;
            return definition != null;
        }

        public IReadOnlyList<MarkDefinition> GetMarks(CombatantState target)
        {
            if (target == null)
            {
                return Array.Empty<MarkDefinition>();
            }

            var slot = target.ActiveMark;
            if (!slot.HasValue || slot.Definition == null)
            {
                return Array.Empty<MarkDefinition>();
            }

            return new[] { slot.Definition };
        }

        public bool TryExpireMark(CombatantState target)
        {
            return ClearMark(target, null, MarkChangeReason.Expired);
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

        private void RaiseEvent(
            CombatantState target,
            MarkDefinition def,
            MarkChangeReason reason,
            CombatantState detonator,
            string reactionId,
            MarkSlot slot)
        {
            if (OnMarkChanged == null)
            {
                return;
            }

            var evt = new MarkEvent(
                target,
                slot.MarkId,
                def,
                reason,
                detonator,
                reactionId,
                slot.AppliedBy,
                slot.RemainingTurns);
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
            string reactionId,
            CombatantState appliedBy,
            int remainingTurns)
        {
            Target = target;
            MarkId = markId;
            Definition = definition;
            Reason = reason;
            Detonator = detonator;
            ReactionId = reactionId;
            AppliedBy = appliedBy;
            RemainingTurns = remainingTurns;
        }

        public CombatantState Target { get; }
        public string MarkId { get; }
        public MarkDefinition Definition { get; }
        public MarkChangeReason Reason { get; }
        public CombatantState Detonator { get; }
        public string ReactionId { get; }
        public CombatantState AppliedBy { get; }
        public int RemainingTurns { get; }
    }
}
