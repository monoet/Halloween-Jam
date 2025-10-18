using BattleV2.Providers;
using UnityEngine;

namespace BattleV2.Anim
{
    /// <summary>
    /// Default tween-based animation strategy for player/enemy combatants.
    /// </summary>
    [CreateAssetMenu(menuName = "BattleV2/Anim/Strategies/Tween Default")]
    public class TweenBattleAnimationStrategy : BattleAnimationStrategy
    {
        [SerializeField] private bool pauseFlowDuringAttack = true;

        public override BattleAnimationResult OnActionSelected(
            BattleAnimationContext context,
            BattleSelection selection,
            int cpBefore)
        {
            var playerController = context.PlayerController;
            if (playerController == null)
            {
                return BattleAnimationResult.None;
            }

            playerController.PlayAttackAnimation();

            var profile = playerController.GetProfile();
            if (pauseFlowDuringAttack && profile != null)
            {
                return BattleAnimationResult.LockFor(profile.TotalDuration());
            }

            return BattleAnimationResult.None;
        }

        public override BattleAnimationResult OnActionResolved(
            BattleAnimationContext context,
            BattleSelection selection,
            int cpBefore,
            int cpAfter)
        {
            context.EnemyController?.PlayHitFeedback();
            return BattleAnimationResult.None;
        }

        public override void OnCombatReset(BattleAnimationContext context)
        {
            context.PlayerController?.ResetToIdle();
            context.EnemyController?.ResetToIdle();
        }
    }
}
