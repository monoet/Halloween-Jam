using System;
using BattleV2.Charge;
using BattleV2.Core;

namespace BattleV2.Actions
{
    public interface IAction
    {
        string Id { get; }
        int CostSP { get; }
        int CostCP { get; }
        ChargeProfile ChargeProfile { get; }

        bool CanExecute(CombatantState actor, CombatContext context, int cpCharge);

        /// <summary>
        /// Executes the action against the current context.
        /// The callback must be invoked once execution finishes to continue the battle loop.
        /// </summary>
        void Execute(CombatantState actor, CombatContext context, int cpCharge, Action onComplete);
    }
}
