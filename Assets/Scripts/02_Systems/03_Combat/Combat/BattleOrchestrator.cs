using System;
using System;
using System.Collections;
using HalloweenJam.Combat.Animations;
using HalloweenJam.Combat.Strategies;
using UnityEngine;

namespace HalloweenJam.Combat
{
    /// <summary>
    /// Runs the combat loop by coordinating turn strategies and attack resolution.
    /// </summary>
    public sealed class BattleOrchestrator : IDisposable
    {
        private readonly MonoBehaviour coroutineRunner;
        private readonly BattleActionResolver actionResolver;
        private readonly BattleTurnStrategyBase playerStrategy;
        private readonly BattleTurnStrategyBase enemyStrategy;
        private readonly IAttackAnimator playerAnimator;
        private readonly IAttackAnimator enemyAnimator;
        private readonly IAttackAnimationPhases playerAnimatorPhases;
        private readonly IAttackAnimationPhases enemyAnimatorPhases;
        private readonly float enemyTurnDelay;

        private ICombatEntity playerEntity;
        private ICombatEntity enemyEntity;
        private Coroutine playerTurnRoutine;
        private Coroutine enemyTurnRoutine;
        private bool battleOver;
        private bool isPlayerTurn;

        public bool IsBusy { get; private set; }

        public BattleOrchestrator(
            MonoBehaviour coroutineRunner,
            BattleActionResolver actionResolver,
            BattleTurnStrategyBase playerStrategy,
            BattleTurnStrategyBase enemyStrategy,
            IAttackAnimator playerAnimator,
            IAttackAnimator enemyAnimator,
            float enemyTurnDelay)
        {
            this.coroutineRunner = coroutineRunner ?? throw new ArgumentNullException(nameof(coroutineRunner));
            this.actionResolver = actionResolver ?? throw new ArgumentNullException(nameof(actionResolver));
            this.playerStrategy = playerStrategy ?? throw new ArgumentNullException(nameof(playerStrategy));
            this.enemyStrategy = enemyStrategy ?? throw new ArgumentNullException(nameof(enemyStrategy));
            this.playerAnimator = playerAnimator;
            this.enemyAnimator = enemyAnimator;
            this.enemyTurnDelay = Mathf.Max(0f, enemyTurnDelay);
            playerAnimatorPhases = playerAnimator as IAttackAnimationPhases;
            enemyAnimatorPhases = enemyAnimator as IAttackAnimationPhases;
        }

        public event Action PlayerTurnReady;
        public event Action PlayerTurnCommitted;
        public event Action EnemyTurnStarted;
        public event Action EnemyTurnCompleted;
        public event Action BattleEnded;
        public event Action<AttackAnimationPhase> PlayerAnimationPhaseChanged;
        public event Action<AttackAnimationPhase> EnemyAnimationPhaseChanged;

        public bool BattleOver => battleOver;
        public bool CanPlayerAct => !battleOver && isPlayerTurn;

        public void Initialize(ICombatEntity player, ICombatEntity enemy)
        {
            playerEntity = player ?? throw new ArgumentNullException(nameof(player));
            enemyEntity = enemy ?? throw new ArgumentNullException(nameof(enemy));

            battleOver = false;
            isPlayerTurn = true;
            SetBusy(false, "Initialize");
            AttachAnimationPhaseEvents();
            PlayerTurnReady?.Invoke();
        }

        public void ExecutePlayerTurn()
        {
            if (!CanPlayerAct || IsBusy)
            {
                return;
            }

            isPlayerTurn = false;
            SetBusy(true, "ExecutePlayerTurn");
            PlayerTurnCommitted?.Invoke();

            if (playerTurnRoutine != null)
            {
                coroutineRunner.StopCoroutine(playerTurnRoutine);
            }

            playerTurnRoutine = coroutineRunner.StartCoroutine(PlayerTurnRoutine());
        }

        public void NotifyBattleEnded()
        {
            if (battleOver)
            {
                return;
            }

            battleOver = true;
            SetBusy(false, "NotifyBattleEnded");
            StopActiveCoroutines();
            BattleEnded?.Invoke();
        }

        public void Dispose()
        {
            StopActiveCoroutines();
            SetBusy(false, "Dispose");
            DetachAnimationPhaseEvents();
        }

        private IEnumerator PlayerTurnRoutine()
        {
            var context = CreateContext(playerEntity, enemyEntity, playerAnimator, 0f);
            yield return ExecuteTurnRoutine(playerStrategy, context, BeginEnemyTurn, "Player turn");
            playerTurnRoutine = null;
        }

        private void BeginEnemyTurn()
        {
            SetBusy(true, "BeginEnemyTurn");
            EnemyTurnStarted?.Invoke();

            if (enemyTurnRoutine != null)
            {
                coroutineRunner.StopCoroutine(enemyTurnRoutine);
            }

            enemyTurnRoutine = coroutineRunner.StartCoroutine(EnemyTurnRoutine());
        }

        private IEnumerator EnemyTurnRoutine()
        {
            var context = CreateContext(enemyEntity, playerEntity, enemyAnimator, enemyTurnDelay);
            yield return ExecuteTurnRoutine(enemyStrategy, context, CompleteEnemyTurn, "Enemy turn");
            enemyTurnRoutine = null;
        }

        private void EnterPlayerTurn()
        {
            if (battleOver)
            {
                SetBusy(false, "EnterPlayerTurn (battle over)");
                return;
            }

            isPlayerTurn = true;
            SetBusy(false, "EnterPlayerTurn");
            PlayerTurnReady?.Invoke();
        }

        private BattleTurnContext CreateContext(ICombatEntity attacker, ICombatEntity defender, IAttackAnimator animator, float delay)
        {
            return new BattleTurnContext(
                attacker,
                defender,
                animator,
                delay,
                () => battleOver,
                actionResolver.ResolveAttack);
        }

        private void StopActiveCoroutines()
        {
            if (playerTurnRoutine != null)
            {
                coroutineRunner.StopCoroutine(playerTurnRoutine);
                playerTurnRoutine = null;
            }

            if (enemyTurnRoutine != null)
            {
                coroutineRunner.StopCoroutine(enemyTurnRoutine);
                enemyTurnRoutine = null;
            }
        }

        private IEnumerator ExecuteTurnRoutine(
            BattleTurnStrategyBase strategy,
            BattleTurnContext context,
            Action onTurnCompleted,
            string label)
        {
            if (strategy == null)
            {
                Debug.LogWarning("[BattleOrchestrator] Turn strategy missing: " + label);
                yield break;
            }

            yield return strategy.ExecuteTurn(context);

            if (battleOver)
            {
                SetBusy(false, $"{label} aborted (battle over)");
                yield break;
            }

            onTurnCompleted?.Invoke();
        }

        private void CompleteEnemyTurn()
        {
            EnemyTurnCompleted?.Invoke();
            EnterPlayerTurn();
        }

        private void SetBusy(bool value, string reason)
        {
            if (IsBusy == value)
            {
                return;
            }

            IsBusy = value;
            Debug.LogFormat("[BattleOrchestrator] IsBusy -> {0} ({1})", value, reason);
        }

        private void AttachAnimationPhaseEvents()
        {
            if (playerAnimatorPhases != null)
            {
                playerAnimatorPhases.PhaseChanged -= HandlePlayerAnimationPhaseChanged;
                playerAnimatorPhases.PhaseChanged += HandlePlayerAnimationPhaseChanged;
            }

            if (enemyAnimatorPhases != null)
            {
                enemyAnimatorPhases.PhaseChanged -= HandleEnemyAnimationPhaseChanged;
                enemyAnimatorPhases.PhaseChanged += HandleEnemyAnimationPhaseChanged;
            }
        }

        private void DetachAnimationPhaseEvents()
        {
            if (playerAnimatorPhases != null)
            {
                playerAnimatorPhases.PhaseChanged -= HandlePlayerAnimationPhaseChanged;
            }

            if (enemyAnimatorPhases != null)
            {
                enemyAnimatorPhases.PhaseChanged -= HandleEnemyAnimationPhaseChanged;
            }
        }

        private void HandlePlayerAnimationPhaseChanged(AttackAnimationPhase phase)
        {
            PlayerAnimationPhaseChanged?.Invoke(phase);
        }

        private void HandleEnemyAnimationPhaseChanged(AttackAnimationPhase phase)
        {
            EnemyAnimationPhaseChanged?.Invoke(phase);
        }
    }
}
