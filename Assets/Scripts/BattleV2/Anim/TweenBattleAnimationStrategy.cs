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
        [SerializeField] private float playerDirection = 1f;
        [SerializeField] private float enemyDirection = -1f;
        [SerializeField] private float enemyAttackDelay = 0.15f;
        [SerializeField] private bool enableDebugLogs = false;

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

            var enemyController = context.EnemyController;

            float playerFacingDir = ResolveDirection(playerController, enemyController, playerDirection);
            playerController.SetDirection(playerFacingDir);
            if (enemyController != null)
            {
                enemyController.SetDirection(ResolveDirection(enemyController, playerController, enemyDirection));
            }
            float enemyPushDir = ResolvePushDirection(playerController, enemyController, playerFacingDir);

            DebugLog("Player attack started", context);
            playerController.PlayAttackAnimation(
                () =>
                {
                    DebugLog("Player strike callback", context);
                    BattleEvents.EmitPlayerAttackStrike();
                    enemyController?.PlayHitFeedback(enemyPushDir);
                },
                () =>
                {
                    DebugLog("Player attack complete", context);
                    BattleEvents.EmitAnimationStageCompleted(BattleAnimationStage.PlayerAttack);
                });

            var profile = playerController.GetProfile();
            if (pauseFlowDuringAttack && profile != null)
            {
                return BattleAnimationResult.LockFor(profile.TotalDuration(), BattleAnimationStage.PlayerAttack);
            }

            return BattleAnimationResult.Immediate(BattleAnimationStage.PlayerAttack);
        }

        public override BattleAnimationResult OnActionResolved(
            BattleAnimationContext context,
            BattleSelection selection,
            int cpBefore,
            int cpAfter)
        {
            var enemyController = context.EnemyController;
            if (enemyController == null)
            {
                return BattleAnimationResult.Immediate(BattleAnimationStage.EnemyAttack);
            }

            var playerController = context.PlayerController;
            float enemyFacingDir = ResolveDirection(enemyController, playerController, enemyDirection);
            enemyController.SetDirection(enemyFacingDir);
            float playerPushDir = ResolvePushDirection(enemyController, playerController, enemyFacingDir);

            float delay = Mathf.Max(0f, enemyAttackDelay);
            var manager = context.Manager;
            System.Action strike = () =>
            {
                playerController?.PlayHitFeedback(playerPushDir);
                BattleEvents.EmitEnemyAttackStrike();
            };
            System.Action complete = () =>
            {
                DebugLog("Enemy attack complete", context);
                BattleEvents.EmitAnimationStageCompleted(BattleAnimationStage.EnemyAttack);
            };

            if (delay > 0f && manager != null)
            {
                DebugLog($"Enemy attack scheduled in {delay:0.###}s", context);
                manager.StartCoroutine(DelayedEnemyAttack(enemyController, delay, strike, complete, context));
            }
            else
            {
                DebugLog("Enemy attack plays immediately", context);
                enemyController.PlayAttackAnimation(strike, complete);
                delay = 0f;
            }

            var profile = enemyController.GetProfile();
            if (!pauseFlowDuringAttack || profile == null)
            {
                if (delay > 0f)
                {
                    return BattleAnimationResult.LockFor(delay, BattleAnimationStage.EnemyAttack);
                }

                return BattleAnimationResult.Immediate(BattleAnimationStage.EnemyAttack);
            }

            float total = delay + profile.TotalDuration();
            return total > 0f
                ? BattleAnimationResult.LockFor(total, BattleAnimationStage.EnemyAttack)
                : BattleAnimationResult.Immediate(BattleAnimationStage.EnemyAttack);
        }

        public override void OnCombatReset(BattleAnimationContext context)
        {
            context.PlayerController?.ResetToIdle();
            context.EnemyController?.ResetToIdle();
        }

        private System.Collections.IEnumerator DelayedEnemyAttack(
            BattleAnimationController controller,
            float delay,
            System.Action onStrike,
            System.Action onComplete,
            BattleAnimationContext context)
        {
            yield return new WaitForSeconds(delay);
            if (controller == null)
            {
                yield break;
            }

            DebugLog("Enemy delayed attack triggered", context);
            controller.PlayAttackAnimation(onStrike, onComplete);
        }

        private void DebugLog(string message, BattleAnimationContext context)
        {
            if (!enableDebugLogs)
            {
                return;
            }

            string managerName = context.Manager != null ? context.Manager.name : "null";
            Debug.Log($"[TweenBattleAnimationStrategy] {message} (Manager:{managerName})");
        }

        private float ResolveDirection(BattleAnimationController actor, BattleAnimationController target, float fallback)
        {
            float resolved = 0f;

            if (actor != null && target != null)
            {
                float delta = target.WorldPosition.x - actor.WorldPosition.x;
                if (!Mathf.Approximately(delta, 0f))
                {
                    resolved = Mathf.Sign(delta);
                }
            }

            if (Mathf.Approximately(resolved, 0f))
            {
                resolved = Mathf.Approximately(fallback, 0f) ? 1f : Mathf.Sign(fallback);
            }

            return resolved;
        }

        private float ResolvePushDirection(BattleAnimationController source, BattleAnimationController target, float fallback)
        {
            float resolved = 0f;

            if (source != null && target != null)
            {
                float delta = target.WorldPosition.x - source.WorldPosition.x;
                if (!Mathf.Approximately(delta, 0f))
                {
                    resolved = Mathf.Sign(delta);
                }
            }

            if (Mathf.Approximately(resolved, 0f))
            {
                resolved = Mathf.Approximately(fallback, 0f) ? 1f : Mathf.Sign(fallback);
            }

            return resolved;
        }
    }
}
