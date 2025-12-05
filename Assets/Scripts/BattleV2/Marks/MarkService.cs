using System;
using System.Collections.Generic;
using BattleV2.Core;

namespace BattleV2.Marks
{
    /// <summary>
    /// Maintains a single mark slot per combatant and raises events on changes.
    /// No gameplay logic beyond storage/notifications.
    /// </summary>
    public sealed class MarkService
    {
        private readonly Dictionary<CombatantState, MarkDefinition> marks = new();

        public event Action<CombatantState, MarkDefinition> OnMarkApplied;
        public event Action<CombatantState> OnMarkCleared;

        public bool ApplyMark(CombatantState target, MarkDefinition definition)
        {
            if (target == null || definition == null)
            {
                return false;
            }

            marks[target] = definition;
            OnMarkApplied?.Invoke(target, definition);
            return true;
        }

        public bool ClearMark(CombatantState target)
        {
            if (target == null)
            {
                return false;
            }

            bool hadMark = marks.Remove(target);
            if (hadMark)
            {
                OnMarkCleared?.Invoke(target);
            }

            return hadMark;
        }

        public bool HasMark(CombatantState target) =>
            target != null && marks.ContainsKey(target);

        public bool TryGetMark(CombatantState target, out MarkDefinition definition)
        {
            if (target == null)
            {
                definition = null;
                return false;
            }

            return marks.TryGetValue(target, out definition);
        }
    }
}
