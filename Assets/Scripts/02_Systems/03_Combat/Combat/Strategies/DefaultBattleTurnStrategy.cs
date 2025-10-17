using System.Collections;
using HalloweenJam.Combat.Animations;
using UnityEngine;

namespace HalloweenJam.Combat.Strategies
{
    /// <summary>
    /// Default turn strategy: optional delay, animated attack (if present), and damage resolution on impact.
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Battle/Default Turn Strategy")]
    public sealed class DefaultBattleTurnStrategy : BattleTurnStrategyBase
    {
        public override IEnumerator ExecuteTurn(Legacy.CombatV1.Combat.BattleTurnContext context)
        {
            if (context.IsBattleOver())
            {
                yield break;
            }

            if (context.PreAttackDelay > 0f)
            {
                yield return new WaitForSeconds(context.PreAttackDelay);

                if (context.IsBattleOver())
                {
                    yield break;
                }
            }

            var animator = context.Animator;
            if (animator != null)
            {
                var animationCompleted = false;
                animator.PlayAttack(
                    onImpact: () =>
                    {
                        if (!context.IsBattleOver())
                        {
                            context.ResolveAttack();
                        }
                    },
                    onComplete: () => animationCompleted = true);

                while (!animationCompleted && !context.IsBattleOver())
                {
                    yield return null;
                }
            }
            else
            {
                context.ResolveAttack();
            }
        }
    }
}
