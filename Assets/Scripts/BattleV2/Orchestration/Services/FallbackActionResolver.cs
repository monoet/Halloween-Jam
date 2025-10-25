using BattleV2.Actions;
using BattleV2.Core;
using UnityEngine;

namespace BattleV2.Orchestration.Services
{
    public interface IFallbackActionResolver
    {
        bool TryResolve(CombatantState actor, CombatContext context, out BattleSelection selection);
    }

    public sealed class FallbackActionResolver : IFallbackActionResolver
    {
        private readonly ActionCatalog actionCatalog;
        private readonly ICombatantActionValidator actionValidator;

        public FallbackActionResolver(ActionCatalog actionCatalog, ICombatantActionValidator actionValidator)
        {
            this.actionCatalog = actionCatalog;
            this.actionValidator = actionValidator;
        }

        public bool TryResolve(CombatantState actor, CombatContext context, out BattleSelection selection)
        {
            selection = default;

            if (actionCatalog == null || actor == null)
            {
                return false;
            }

            var fallback = actionCatalog.Fallback(actor, context);
            if (fallback == null)
            {
                Debug.LogWarning("[FallbackActionResolver] Fallback action is null.");
                return false;
            }

            if (!actionValidator.TryValidate(fallback, actor, context, 0, out var implementation))
            {
                Debug.LogWarning($"[FallbackActionResolver] Fallback action invalid: {fallback.id}.");
                return false;
            }

            selection = new BattleSelection(fallback, 0, implementation.ChargeProfile, null);
            return true;
        }
    }
}
