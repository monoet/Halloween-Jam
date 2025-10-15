using System;
using UnityEngine;

namespace HalloweenJam.Combat
{
    /// <summary>
    /// Handles battle completion logic and exposes an event for external systems.
    /// </summary>
    public sealed class BattleOutcomeController : IDisposable
    {
        private readonly BattleUIController uiController;
        private readonly BattleMusicController musicController;
        private readonly bool destroyDefeatedEntities;
        private readonly MonoBehaviour playerSource;
        private readonly MonoBehaviour enemySource;
        private readonly Action<string, object[]> debugLogger;

        private BattleOrchestrator orchestrator;
        private ICombatEntity playerEntity;
        private ICombatEntity enemyEntity;
        private bool outcomeResolved;

        public BattleOutcomeController(
            BattleUIController uiController,
            BattleMusicController musicController,
            bool destroyDefeatedEntities,
            MonoBehaviour playerSource,
            MonoBehaviour enemySource,
            Action<string, object[]> debugLogger)
        {
            this.uiController = uiController;
            this.musicController = musicController;
            this.destroyDefeatedEntities = destroyDefeatedEntities;
            this.playerSource = playerSource;
            this.enemySource = enemySource;
            this.debugLogger = debugLogger;
        }

        public event Action<BattleOutcome> BattleFinished;

        public void Initialize(BattleOrchestrator battleOrchestrator, ICombatEntity player, ICombatEntity enemy)
        {
            Dispose();

            orchestrator = battleOrchestrator ?? throw new ArgumentNullException(nameof(battleOrchestrator));
            playerEntity = player ?? throw new ArgumentNullException(nameof(player));
            enemyEntity = enemy ?? throw new ArgumentNullException(nameof(enemy));
            outcomeResolved = false;

            playerEntity.OnDefeated += HandleEntityDefeated;
            enemyEntity.OnDefeated += HandleEntityDefeated;
        }

        public void Dispose()
        {
            if (playerEntity != null)
            {
                playerEntity.OnDefeated -= HandleEntityDefeated;
                playerEntity = null;
            }

            if (enemyEntity != null)
            {
                enemyEntity.OnDefeated -= HandleEntityDefeated;
                enemyEntity = null;
            }

            orchestrator = null;
            outcomeResolved = false;
        }

        private void HandleEntityDefeated(ICombatEntity defeated)
        {
            if (outcomeResolved)
            {
                return;
            }

            outcomeResolved = true;
            Debug("Battle outcome resolved. Defeated entity: {0}", defeated.DisplayName);

            orchestrator?.NotifyBattleEnded();

            uiController.HideActionMenu();
            uiController.WriteLog($"{defeated.DisplayName} has been defeated!");
            uiController.ShowVictory(defeated);

            musicController?.PlayVictoryMusic();
            DestroyEntity(defeated);

            var outcome = BuildOutcome(defeated);
            BattleFinished?.Invoke(outcome);
        }

        private BattleOutcome BuildOutcome(ICombatEntity defeated)
        {
            if (defeated == enemyEntity)
            {
                return new BattleOutcome(BattleVictory.PlayerVictory, playerEntity, enemyEntity);
            }

            if (defeated == playerEntity)
            {
                return new BattleOutcome(BattleVictory.PlayerDefeat, enemyEntity, playerEntity);
            }

            return new BattleOutcome(BattleVictory.Unknown, null, defeated);
        }

        private void DestroyEntity(ICombatEntity defeated)
        {
            if (!destroyDefeatedEntities || defeated == null)
            {
                return;
            }

            if (defeated == playerEntity && playerSource != null)
            {
                Debug("Destroying defeated player entity GameObject.");
                UnityEngine.Object.Destroy(playerSource.gameObject);
            }
            else if (defeated == enemyEntity && enemySource != null)
            {
                Debug("Destroying defeated enemy entity GameObject.");
                UnityEngine.Object.Destroy(enemySource.gameObject);
            }
        }

        private void Debug(string message, params object[] args)
        {
            debugLogger?.Invoke(message, args);
        }
    }
}

