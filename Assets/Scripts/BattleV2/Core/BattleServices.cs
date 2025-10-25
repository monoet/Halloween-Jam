using HalloweenJam.Combat.Animations;
using UnityEngine;

namespace BattleV2.Core
{
    /// <summary>
    /// Provides shared services (animators, rng, etc.) to actions and orchestrator.
    /// </summary>
    [System.Serializable]
    public class BattleServices
    {
        public System.Random Rng { get; } = new System.Random();

        /// <summary>
        /// Attempts to get an attack animator bound to the supplied combatant.
        /// </summary>
        public IAttackAnimator GetAnimatorFor(CombatantState combatant)
        {
            if (combatant == null)
            {
                return null;
            }

            if (combatant.TryGetComponent(out IAttackAnimator animator))
            {
                return animator;
            }

            BattleLogger.Warn("Services", $"No attack animator found for {combatant.name}.");
            return null;
        }
    }
}
