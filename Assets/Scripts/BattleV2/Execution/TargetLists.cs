using System;
using System.Collections.Generic;
using BattleV2.Core;

namespace BattleV2.Execution
{
    /// <summary>
    /// Immutable snapshot of oriented target lists for P2-lite.
    /// </summary>
    public readonly struct TargetLists
    {
        public TargetLists(CombatantState[] sameSide, CombatantState[] opponents, bool selfInOpponents)
        {
            SameSide = sameSide ?? Array.Empty<CombatantState>();
            Opponents = opponents ?? Array.Empty<CombatantState>();
            SelfInOpponents = selfInOpponents;
        }

        public CombatantState[] SameSide { get; }
        public CombatantState[] Opponents { get; }
        public bool SelfInOpponents { get; }

        public static TargetLists BuildFromAttacker(
            CombatantState attacker,
            IReadOnlyList<CombatantState> party,
            IReadOnlyList<CombatantState> enemies)
        {
            bool attackerIsEnemy = attacker != null && attacker.IsEnemy;
            var sameSide = attackerIsEnemy ? enemies : party;
            var opponents = attackerIsEnemy ? party : enemies;

            var sameSideSnapshot = TargetSnapshot.Snapshot(sameSide);
            var opponentsSnapshot = TargetSnapshot.Snapshot(opponents);
            bool selfInOpponents = IndexOf(opponentsSnapshot, attacker) >= 0;

            return new TargetLists(sameSideSnapshot, opponentsSnapshot, selfInOpponents);
        }

        private static int IndexOf(IReadOnlyList<CombatantState> list, CombatantState target)
        {
            if (list == null || list.Count == 0 || target == null) return -1;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == target) return i;
            }
            return -1;
        }

    }
}
