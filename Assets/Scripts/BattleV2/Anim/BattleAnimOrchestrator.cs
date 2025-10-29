using System.Collections;
using System.Collections.Generic;
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
        [SerializeField] private bool autoBindControllers = true;
        [SerializeField] private BattleAnimationStrategy sharedStrategy;
        [SerializeField] private BattleAnimationStrategy playerStrategyOverride;
        [SerializeField] private BattleAnimationStrategy enemyStrategyOverride;

        private BattleAnimationContext context;
        private Coroutine lockCoroutine;
        private bool lockActive;
        private BattleAnimationStage currentLockStage = BattleAnimationStage.None;
        private bool stageCompletionSignaled;

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
            if (autoBindControllers)
            {
                manager.OnCombatantsBound += HandleCombatantsBound;
            }
            BattleEvents.OnCombatReset += HandleCombatReset;
            BattleEvents.OnAnimationStageCompleted += HandleAnimationStageCompleted;
        }

        private void Unsubscribe()
        {
            if (manager != null)
            {
                manager.OnPlayerActionSelected -= HandlePlayerActionSelected;
                manager.OnPlayerActionResolved -= HandlePlayerActionResolved;
                if (autoBindControllers)
                {
                    manager.OnCombatantsBound -= HandleCombatantsBound;
                }
            }

            BattleEvents.OnCombatReset -= HandleCombatReset;
            BattleEvents.OnAnimationStageCompleted -= HandleAnimationStageCompleted;
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

        private void HandleCombatantsBound(IReadOnlyList<CombatantState> allies, IReadOnlyList<CombatantState> enemies)
        {
            var playerState = allies != null && allies.Count > 0 ? allies[0] : null;
            var enemyState = enemies != null && enemies.Count > 0 ? enemies[0] : null;
            bool changed = false;

            var resolvedPlayer = ResolveController(playerState, playerAnim);
            if (resolvedPlayer != playerAnim)
            {
                playerAnim = resolvedPlayer;
                changed = true;
            }

            var resolvedEnemy = ResolveController(enemyState, enemyAnim);
            if (resolvedEnemy != enemyAnim)
            {
                enemyAnim = resolvedEnemy;
                changed = true;
            }

            if (changed)
            {
                RefreshContext();
                InvokeResetStrategies();
            }
        }

        private static BattleAnimationController ResolveController(CombatantState state, BattleAnimationController current)
        {
            if (state == null)
            {
                return null;
            }

            if (current != null && current.gameObject.scene.IsValid())
            {
                return current;
            }

            BattleAnimationController controller = null;

            // Try the same GameObject first (common case after refactors).
            controller = state.GetComponent<BattleAnimationController>();
            if (controller != null)
            {
                return controller;
            }

            // Fallback to children (original prefab layout).
            controller = state.GetComponentInChildren<BattleAnimationController>();
            if (controller != null)
            {
                return controller;
            }

            // Some prefabs keep the animation controller on the parent or on the runtime root.
            controller = state.GetComponentInParent<BattleAnimationController>();
            if (controller != null)
            {
                return controller;
            }

            var runtime = state.CharacterRuntime != null
                ? state.CharacterRuntime.GetComponentInChildren<BattleAnimationController>()
                : null;

            return runtime;
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
            if (result.Stage != BattleAnimationStage.None && !result.RequestLock)
            {
                BattleEvents.EmitAnimationStageCompleted(result.Stage);
            }

            if (!result.RequestLock)
            {
                return;
            }

            if (lockCoroutine != null)
            {
                StopCoroutine(lockCoroutine);
            }

            currentLockStage = result.Stage;
            stageCompletionSignaled = false;
            lockCoroutine = StartCoroutine(HandleLock(result.LockDurationSeconds));
        }

        private IEnumerator HandleLock(float durationSeconds)
        {
            if (!lockActive)
            {
                lockActive = true;
                BattleEvents.EmitLockChanged(true);
            }

            float wait = Mathf.Max(0f, durationSeconds);
            if (wait > 0f)
            {
                yield return new WaitForSeconds(wait);
            }
            else
            {
                yield return null;
            }

            if (!stageCompletionSignaled && currentLockStage != BattleAnimationStage.None)
            {
                stageCompletionSignaled = true;
                BattleEvents.EmitAnimationStageCompleted(currentLockStage);
            }

            lockActive = false;
            BattleEvents.EmitLockChanged(false);
            currentLockStage = BattleAnimationStage.None;
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

            currentLockStage = BattleAnimationStage.None;
            stageCompletionSignaled = true;
        }

        private void HandleAnimationStageCompleted(BattleAnimationStage stage)
        {
            if (stage == BattleAnimationStage.None)
            {
                return;
            }

            if (currentLockStage != BattleAnimationStage.None && stage != currentLockStage)
            {
                return;
            }

            stageCompletionSignaled = true;
            ReleaseLockImmediate();
        }

        private void OnValidate()
        {
            RefreshContext();
        }
    }
}
