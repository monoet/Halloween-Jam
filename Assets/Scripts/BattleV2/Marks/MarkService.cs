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
            int appliedById = 0,
            int appliedAtOwnerTurnCounter = 0,
            int remainingTurns = 1,
            int executionId = 0)
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
                    Debug.LogWarning($"[MarkService] Attempted to apply mark '{key}' on occupied slot '{current.MarkId}'. Overwrite disallowed. target={target?.name}");
                    return false;
                }
            }

            if (appliedById == 0 || appliedAtOwnerTurnCounter == 0)
            {
                Debug.LogWarning($"[MarkService] ApplyMark missing appliedBy/turn info. appliedById={appliedById} turn={appliedAtOwnerTurnCounter} target={target?.name}");
            }

            var slot = new MarkSlot(definition, appliedById, appliedAtOwnerTurnCounter, remainingTurns);
            target.SetMarkSlot(slot);
            RaiseEvent(target, definition, reason, appliedById, null, slot, executionId);
            return true;
        }

        public bool ClearMark(CombatantState target, string markId = null, MarkChangeReason reason = MarkChangeReason.Cleared, int executionId = 0)
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
            RaiseEvent(target, slot.Definition, reason, slot.AppliedById, null, slot, executionId);
            return true;
        }

        public bool DetonateMark(CombatantState target, string markId, int detonatorId = 0, string reactionId = null, int executionId = 0)
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
            int resolvedDetonator = detonatorId != 0 ? detonatorId : slot.AppliedById;
            RaiseEvent(target, slot.Definition, MarkChangeReason.Detonated, resolvedDetonator, reactionId, slot, executionId);
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

        public bool TryExpireMarkForOwnerTurn(CombatantState target, int ownerId, int ownerTurnCounter, int executionId = 0)
        {
            if (target == null)
            {
                return false;
            }

            var slot = target.ActiveMark;
            if (!slot.HasValue)
            {
                return false;
            }

            if (slot.AppliedById != ownerId)
            {
                return false;
            }

            // MVP: expira cuando el owner vuelve a tomar turno (turnCounter mayor al de aplicación).
            if (ownerTurnCounter <= slot.AppliedAtOwnerTurnCounter)
            {
                return false;
            }

            return ClearMark(target, null, MarkChangeReason.Expired, executionId);
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
            int detonatorId,
            string reactionId,
            MarkSlot slot,
            int executionId)
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
                detonatorId,
                reactionId,
                slot.AppliedById,
                slot.RemainingTurns,
                executionId);
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
            int detonatorId,
            string reactionId,
            int appliedById,
            int remainingTurns,
            int executionId)
        {
            Target = target;
            MarkId = markId;
            Definition = definition;
            Reason = reason;
            DetonatorId = detonatorId;
            ReactionId = reactionId;
            AppliedById = appliedById;
            RemainingTurns = remainingTurns;
            ExecutionId = executionId;
        }

        public CombatantState Target { get; }
        public string MarkId { get; }
        public MarkDefinition Definition { get; }
        public MarkChangeReason Reason { get; }
        public int DetonatorId { get; }
        public string ReactionId { get; }
        public int AppliedById { get; }
        public int RemainingTurns { get; }
        public int ExecutionId { get; }
    }
}
