using BattleV2.Actions;
using BattleV2.Core;
using UnityEngine;

namespace BattleV2.Orchestration.Services
{
    public interface ICombatantActionValidator
    {
        bool TryValidate(
            BattleActionData action,
            CombatantState actor,
            CombatContext context,
            int cpCharge,
            out IAction implementation);
    }

    /// <summary>
    /// Validates action availability (cost, SP/CP, CanExecute) and resolves implementation.
    /// </summary>
    public sealed class CombatantActionValidator : ICombatantActionValidator
    {
        private readonly ActionCatalog catalog;

        public CombatantActionValidator(ActionCatalog catalog)
        {
            this.catalog = catalog;
        }

        public bool TryValidate(
            BattleActionData action,
            CombatantState actor,
            CombatContext context,
            int cpCharge,
            out IAction implementation)
        {
            implementation = null;

            if (catalog == null || action == null || actor == null)
            {
                return false;
            }

            implementation = catalog.Resolve(action);
            if (implementation == null)
            {
                Debug.LogWarning($"[CombatantActionValidator] Action {action.id} missing implementation.");
                return false;
            }

            int totalCpRequired = implementation.CostCP + Mathf.Max(0, cpCharge);
            if (actor.CurrentCP < totalCpRequired)
            {
                return false;
            }

            if (implementation.CostSP > 0 && actor.CurrentSP < implementation.CostSP)
            {
                return false;
            }

            if (!implementation.CanExecute(actor, context, cpCharge))
            {
                return false;
            }

            return true;
        }
    }
}
