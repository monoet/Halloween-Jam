using System.Collections;
using BattleV2.Orchestration;
using BattleV2.Providers;
using UnityEngine;

namespace BattleV2.Anim
{
    /// <summary>
    /// Routes BattleManagerV2 events into animation strategies so the presentation layer stays decoupled.
    /// </summary>
    public class BattleAnimOrchestrator : MonoBehaviour
    {
        [SerializeField] private BattleManagerV2 manager;
        [SerializeField] private BattleAnimationController playerAnim;
        [SerializeField] private BattleAnimationController enemyAnim;
        [SerializeField] private BattleAnimationStrategy sharedStrategy;
        [SerializeField] private BattleAnimationStrategy playerStrategyOverride;
        [SerializeField] private BattleAnimationStrategy enemyStrategyOverride;

        private BattleAnimationContext context;
        private Coroutine lockCoroutine;
        private bool lockActive;

        private BattleAnimationStrategy PlayerStrategy => playerStrategyOverride != null ? playerStrategyOverride : sharedStrategy;
        private BattleAnimationStrategy EnemyStrategy => enemyStrategyOverride != null ? enemyStrategyOverride : sharedStrategy;

        private void Awake()
        {
            RefreshContext();
        }

        private void OnEnable()
        {
            RefreshContext();
            Subscribe();
            InvokeResetStrategies();
        }

        private void OnDisable()
        {
            Unsubscribe();
            ReleaseLockImmediate();
        }

        private void RefreshContext()
        {
            context = new BattleAnimationContext(manager, playerAnim, enemyAnim);
        }

        private void Subscribe()
        {
            if (manager == null)
            {
                return;
            }

            manager.OnPlayerActionSelected += HandlePlayerActionSelected;
            manager.OnPlayerActionResolved += HandlePlayerActionResolved;
            BattleEvents.OnCombatReset += HandleCombatReset;
        }

        private void Unsubscribe()
        {
            if (manager != null)
            {
                manager.OnPlayerActionSelected -= HandlePlayerActionSelected;
                manager.OnPlayerActionResolved -= HandlePlayerActionResolved;
            }

            BattleEvents.OnCombatReset -= HandleCombatReset;
        }

        private void HandlePlayerActionSelected(BattleSelection selection, int cpBefore)
        {
            var result = PlayerStrategy != null
                ? PlayerStrategy.OnActionSelected(context, selection, cpBefore)
                : BattleAnimationResult.None;

            ApplyResult(result);
        }

        private void HandlePlayerActionResolved(BattleSelection selection, int cpBefore, int cpAfter)
        {
            var result = EnemyStrategy != null
                ? EnemyStrategy.OnActionResolved(context, selection, cpBefore, cpAfter)
                : BattleAnimationResult.None;

            ApplyResult(result);
        }

        private void HandleCombatReset()
        {
            ReleaseLockImmediate();
            InvokeResetStrategies();
        }

        private void InvokeResetStrategies()
        {
            PlayerStrategy?.OnCombatReset(context);
            if (EnemyStrategy != PlayerStrategy)
            {
                EnemyStrategy?.OnCombatReset(context);
            }
        }

        private void ApplyResult(BattleAnimationResult result)
        {
            if (!result.RequestLock)
            {
                return;
            }

            if (lockCoroutine != null)
            {
                StopCoroutine(lockCoroutine);
            }

            lockCoroutine = StartCoroutine(HandleLock(result.LockDurationSeconds));
        }

        private IEnumerator HandleLock(float durationSeconds)
        {
            if (!lockActive)
            {
                lockActive = true;
                BattleEvents.EmitLockChanged(true);
            }

            yield return new WaitForSeconds(Mathf.Max(0f, durationSeconds));

            lockActive = false;
            BattleEvents.EmitLockChanged(false);
            lockCoroutine = null;
        }

        private void ReleaseLockImmediate()
        {
            if (lockCoroutine != null)
            {
                StopCoroutine(lockCoroutine);
                lockCoroutine = null;
            }

            if (lockActive)
            {
                lockActive = false;
                BattleEvents.EmitLockChanged(false);
            }
        }

        private void OnValidate()
        {
            RefreshContext();
        }
    }
}
