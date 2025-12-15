using System;
using System.Collections.Generic;
using BattleV2.Core;

namespace BattleV2.Orchestration
{
    /// <summary>
    /// Immutable snapshot of roster/targets for a single action execution.
    /// Use this instead of live lists to ensure deterministic behavior.
    /// </summary>
    public readonly struct ExecutionSnapshot
    {
        public ExecutionSnapshot(
            IReadOnlyList<CombatantState> allies,
            IReadOnlyList<CombatantState> enemies,
            IReadOnlyList<CombatantState> targets)
        {
            Allies = allies != null ? new List<CombatantState>(allies).ToArray() : Array.Empty<CombatantState>();
            Enemies = enemies != null ? new List<CombatantState>(enemies).ToArray() : Array.Empty<CombatantState>();
            Targets = targets != null ? new List<CombatantState>(targets).ToArray() : Array.Empty<CombatantState>();
        }

        public IReadOnlyList<CombatantState> Allies { get; }
        public IReadOnlyList<CombatantState> Enemies { get; }
        public IReadOnlyList<CombatantState> Targets { get; }
    }
}
