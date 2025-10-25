using BattleV2.Providers;
using UnityEngine;

namespace BattleV2.Anim
{
    /// <summary>
    /// Base contract for animation strategies that react to battle events.
    /// </summary>
    public abstract class BattleAnimationStrategy : ScriptableObject
    {
        public abstract BattleAnimationResult OnActionSelected(
            BattleAnimationContext context,
            BattleSelection selection,
            int cpBefore);

        public abstract BattleAnimationResult OnActionResolved(
            BattleAnimationContext context,
            BattleSelection selection,
            int cpBefore,
            int cpAfter);

        public virtual void OnCombatReset(BattleAnimationContext context)
        {
        }
    }
}
