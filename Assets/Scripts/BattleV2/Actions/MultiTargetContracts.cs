using System;
using System.Collections.Generic;
using BattleV2.Core;
using BattleV2.Providers;

namespace BattleV2.Actions
{
    public enum MultiImpactStrategy
    {
        Simultaneous,
        Sequential
    }

    public enum TimedHitStrategy
    {
        SingleCheck,
        PerTarget
    }

    /// <summary>
    /// Optional multi-target contract for actions that can operate on a resolved target list (Single o All).
    /// </summary>
    public interface IActionMultiTarget
    {
        void ExecuteMulti(
            CombatantState actor,
            CombatContext context,
            IReadOnlyList<CombatantState> targets,
            BattleSelection selection,
            Action onComplete);
    }
}
