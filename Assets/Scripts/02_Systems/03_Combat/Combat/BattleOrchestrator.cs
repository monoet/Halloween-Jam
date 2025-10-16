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
        }

        public event Action PlayerTurnReady;
        public event Action PlayerTurnCommitted;
        public event Action EnemyTurnStarted;
        public event Action EnemyTurnCompleted;
        public event Action BattleEnded;

        public bool BattleOver => battleOver;
        public bool CanPlayerAct => !battleOver && isPlayerTurn;

        public void Initialize(ICombatEntity player, ICombatEntity enemy)
        {
            playerEntity = player ?? throw new ArgumentNullException(nameof(player));
            enemyEntity = enemy ?? throw new ArgumentNullException(nameof(enemy));

            battleOver = false;
            isPlayerTurn = true;
            IsBusy = false;
            PlayerTurnReady?.Invoke();
        }

        public void ExecutePlayerTurn()
        {
            if (!CanPlayerAct || IsBusy)
            {
                return;
            }

            isPlayerTurn = false;
            IsBusy = true;
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
            IsBusy = false;
            StopActiveCoroutines();
            BattleEnded?.Invoke();
        }

        public void Dispose()
        {
            StopActiveCoroutines();
        }

        private IEnumerator PlayerTurnRoutine()
        {
            var context = CreateContext(playerEntity, enemyEntity, playerAnimator, 0f);
            yield return playerStrategy.ExecuteTurn(context);

            playerTurnRoutine = null;

            if (battleOver)
            {
                IsBusy = false;
                yield break;
            }

            BeginEnemyTurn();
        }

        private void BeginEnemyTurn()
        {
            IsBusy = true;
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
            yield return enemyStrategy.ExecuteTurn(context);

            enemyTurnRoutine = null;
            EnemyTurnCompleted?.Invoke();

            EnterPlayerTurn();
        }

        private void EnterPlayerTurn()
        {
            if (battleOver)
            {
                IsBusy = false;
                return;
            }

            isPlayerTurn = true;
            IsBusy = false;
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
    }
}
