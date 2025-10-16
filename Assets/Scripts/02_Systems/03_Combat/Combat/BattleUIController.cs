using System;
using HalloweenJam.UI.Combat;
using TMPro;
using UnityEngine;

namespace HalloweenJam.Combat
{
    /// <summary>
    /// Handles combat-specific UI updates and listens to battle events.
    /// </summary>
    public sealed class BattleUIController
    {
        private readonly BattleHUD playerHud;
        private readonly BattleHUD enemyHud;
        private readonly TMP_Text battleLog;
        private readonly BattleActionMenu actionMenu;
        private readonly GameObject victoryScreen;
        private readonly TMP_Text victoryMessage;
        private readonly TMP_Text victoryReward;
        private readonly string victoryRewardFormat;
        private readonly string defeatRewardText;
        private readonly string playerVictoryMessage;
        private readonly string playerDefeatMessage;
        private readonly Action<string, object[]> debugLogger;

        private ICombatEntity playerEntity;
        private ICombatEntity enemyEntity;
        private BattleActionResolver actionResolver;
        private BattleOrchestrator orchestrator;

        public BattleUIController(
            BattleHUD playerHud,
            BattleHUD enemyHud,
            TMP_Text battleLog,
            BattleActionMenu actionMenu,
            GameObject victoryScreen,
            TMP_Text victoryMessage,
            TMP_Text victoryReward,
            string victoryRewardFormat,
            string defeatRewardText,
            string playerVictoryMessage,
            string playerDefeatMessage,
            Action<string, object[]> debugLogger)
        {
            this.playerHud = playerHud;
            this.enemyHud = enemyHud;
            this.battleLog = battleLog;
            this.actionMenu = actionMenu;
            this.victoryScreen = victoryScreen;
            this.victoryMessage = victoryMessage;
            this.victoryReward = victoryReward;
            this.victoryRewardFormat = victoryRewardFormat;
            this.defeatRewardText = defeatRewardText;
            this.playerVictoryMessage = playerVictoryMessage;
            this.playerDefeatMessage = playerDefeatMessage;
            this.debugLogger = debugLogger;
        }

        public void Initialize(ICombatEntity player, ICombatEntity enemy)
        {
            playerEntity = player;
            enemyEntity = enemy;

            playerHud?.Bind(playerEntity);
            enemyHud?.Bind(enemyEntity);
            Debug("HUDs bound.");

            HideActionMenu();
            ClearBattleLog();
            HideVictoryScreen();
            playerHud?.ForceRefresh();
            enemyHud?.ForceRefresh();
        }

        public void Attach(BattleOrchestrator targetOrchestrator, BattleActionResolver resolver)
        {
            Detach();

            orchestrator = targetOrchestrator;
            actionResolver = resolver;

            if (actionResolver != null)
            {
                actionResolver.AttackResolved += HandleAttackResolved;
            }

            if (orchestrator != null)
            {
                orchestrator.PlayerTurnReady += HandlePlayerTurnReady;
                orchestrator.PlayerTurnCommitted += HandlePlayerTurnCommitted;
                orchestrator.EnemyTurnStarted += HandleEnemyTurnStarted;
                orchestrator.EnemyTurnCompleted += HandleEnemyTurnCompleted;
                orchestrator.BattleEnded += HandleBattleEnded;
            }
        }

        public void Detach()
        {
            if (actionResolver != null)
            {
                actionResolver.AttackResolved -= HandleAttackResolved;
                actionResolver = null;
            }

            if (orchestrator != null)
            {
                orchestrator.PlayerTurnReady -= HandlePlayerTurnReady;
                orchestrator.PlayerTurnCommitted -= HandlePlayerTurnCommitted;
                orchestrator.EnemyTurnStarted -= HandleEnemyTurnStarted;
                orchestrator.EnemyTurnCompleted -= HandleEnemyTurnCompleted;
                orchestrator.BattleEnded -= HandleBattleEnded;
                orchestrator = null;
            }
        }

        public void ShowEngagementMessage()
        {
            if (playerEntity == null || enemyEntity == null)
            {
                return;
            }

            WriteLog($"{playerEntity.DisplayName} engages {enemyEntity.DisplayName}!");
        }

        public void WriteLog(string message)
        {
            if (battleLog == null)
            {
                Debug("WriteLog skipped: battleLog missing.");
                return;
            }

            battleLog.text = message ?? string.Empty;
            Debug("WriteLog: {0}", message);
        }

        public void ShowActionMenu()
        {
            actionMenu?.ShowMenu();
            Debug("Action menu shown.");
        }

        public void HideActionMenu()
        {
            actionMenu?.HideMenu();
            Debug("Action menu hidden.");
        }

        public void ShowVictory(ICombatEntity defeatedEntity)
        {
            if (victoryScreen == null)
            {
                return;
            }

            var message = BuildVictoryMessage(defeatedEntity);
            if (victoryMessage != null)
            {
                victoryMessage.text = message;
            }

            UpdateVictoryRewards(defeatedEntity);
            victoryScreen.SetActive(true);
            Debug("Victory screen shown: '{0}'", message);
        }

        public void HideVictoryScreen()
        {
            if (victoryScreen != null)
            {
                victoryScreen.SetActive(false);
            }

            if (victoryMessage != null)
            {
                victoryMessage.text = string.Empty;
            }

            if (victoryReward != null)
            {
                victoryReward.text = string.Empty;
            }

            Debug("Victory screen hidden.");
        }

        private void HandleAttackResolved(AttackResolutionContext context)
        {
            WriteLog(context.LogMessage);
            Debug("Attack resolved: {0} dealt {1} to {2} (HP {3}/{4}).",
                context.Attacker.DisplayName,
                context.Result.Damage,
                context.Defender.DisplayName,
                context.Defender.CurrentHp,
                context.Defender.MaxHp);
            playerHud?.ForceRefresh();
            enemyHud?.ForceRefresh();
        }

        private void HandlePlayerTurnReady()
        {
            ShowActionMenu();
            Debug("Player turn ready.");
        }

        private void HandlePlayerTurnCommitted()
        {
            HideActionMenu();
            Debug("Player turn committed.");
        }

        private void HandleEnemyTurnStarted()
        {
            HideActionMenu();
            Debug("Enemy turn started.");
        }

        private void HandleEnemyTurnCompleted()
        {
            Debug("Enemy turn completed.");
        }

        private void HandleBattleEnded()
        {
            HideActionMenu();
            Debug("Battle ended.");
        }

        private void ClearBattleLog()
        {
            if (battleLog != null)
            {
                battleLog.text = string.Empty;
            }

            Debug("Battle log cleared.");
        }

        private string BuildVictoryMessage(ICombatEntity defeatedEntity)
        {
            if (defeatedEntity == enemyEntity)
            {
                return playerVictoryMessage;
            }

            if (defeatedEntity == playerEntity)
            {
                return playerDefeatMessage;
            }

            return "Battle Finished";
        }

        private void UpdateVictoryRewards(ICombatEntity defeatedEntity)
        {
            if (victoryReward == null)
            {
                return;
            }

            if (defeatedEntity == enemyEntity && enemyEntity is IRewardProvider rewardProvider)
            {
                var exp = Mathf.Max(0, rewardProvider.ExperienceReward);
                var z = Mathf.Max(0, rewardProvider.ZReward);
                victoryReward.text = string.Format(victoryRewardFormat, exp, z);
                Debug("Victory rewards set to EXP={0}, Z={1}.", exp, z);
            }
            else if (defeatedEntity == playerEntity)
            {
                victoryReward.text = defeatRewardText;
                Debug("Victory rewards set to defeat text.");
            }
            else
            {
                victoryReward.text = string.Empty;
            }
        }

        private void Debug(string message, params object[] args)
        {
            debugLogger?.Invoke(message, args);
        }
    }
}
