using System.Collections;
using HalloweenJam.Combat.Animations;
using HalloweenJam.Combat.Strategies;
using HalloweenJam.UI.Combat;
using TMPro;
using UnityEngine;

namespace HalloweenJam.Combat
{
    public class BattleManager : MonoBehaviour
    {
        [Header("Bindings")]
        [SerializeField] private MonoBehaviour playerEntitySource;
        [SerializeField] private MonoBehaviour enemyEntitySource;
        [SerializeField] private BattleHUD playerHud;
        [SerializeField] private BattleHUD enemyHud;
        [SerializeField] private TMP_Text battleLogText;
        [SerializeField] private BattleActionMenu playerActionMenu;
        [SerializeField] private LiliaAttackAnimator playerAttackAnimator;
        [SerializeField] private EnemyAttackAnimator enemyAttackAnimator;
        [SerializeField] private GameObject victoryScreen;
        [SerializeField] private TMP_Text victoryMessageText;
        [SerializeField] private TMP_Text victoryRewardText;
        [SerializeField] private string victoryRewardFormat = "+{0} EXP\n+{1} Z";
        [SerializeField] private string defeatRewardText = string.Empty;
        [SerializeField] private string playerVictoryMessage = "Victory!";
        [SerializeField] private string playerDefeatMessage = "Defeat...";
        [SerializeField] private bool destroyDefeatedEntities = true;
        [SerializeField] private bool enableDebugLogs = false;
        [Header("Music")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioClip battleMusicClip;
        [SerializeField] private AudioClip victoryMusicClip;
        [SerializeField, Min(0f)] private float musicFadeDuration = 0.5f;

        [Header("Flow Settings")]
        [SerializeField, Min(0f)] private float enemyTurnDelay = 1f;

        private ICombatEntity playerEntity;
        private ICombatEntity enemyEntity;
        private bool battleOver;
        private bool isPlayerTurn = true;
        private Coroutine enemyTurnRoutine;
        private Coroutine musicRoutine;

        private void Awake()
        {
            playerEntity = ExtractCombatEntity(playerEntitySource, "Player Entity");
            enemyEntity = ExtractCombatEntity(enemyEntitySource, "Enemy Entity");
            DebugLog("Awake: Player entity set? {0} | Enemy entity set? {1}", playerEntity != null, enemyEntity != null);

        }

        private void OnEnable()
        {
            playerActionMenu?.RegisterAttackCallback(OnAttackButton);
            Subscribe(playerEntity);
            Subscribe(enemyEntity);
            DebugLog("OnEnable: Subscriptions registered.");
        }

        private void Start()
        {
            InitializeBattle();
        }

        private void OnDisable()
        {
            playerActionMenu?.ClearAttackCallbacks();

            Unsubscribe(playerEntity);
            Unsubscribe(enemyEntity);
            DebugLog("OnDisable: Subscriptions removed.");
        }

        public void OnAttackButton()
        {
            if (!isPlayerTurn || battleOver)
            {
                DebugLog("OnAttackButton ignored: isPlayerTurn={0}, battleOver={1}", isPlayerTurn, battleOver);
                return;
            }

            DebugLog("OnAttackButton: Player initiated attack.");
            playerActionMenu?.HideMenu();

            if (playerAttackAnimator != null)
            {
                playerAttackAnimator.PlayAttack(
                    onImpact: () => ResolveAttack(playerEntity, enemyEntity),
                    onComplete: () =>
                    {
                        if (!battleOver)
                        {
                            BeginEnemyTurn();
                        }
                    });
            }
            else
            {
                ResolveAttack(playerEntity, enemyEntity);

                if (!battleOver)
                {
                    BeginEnemyTurn();
                }
            }
        }

        private void InitializeBattle()
        {
            if (playerEntity == null || enemyEntity == null)
            {
                Debug.LogError("BattleManager cannot initialize without combat entities.", this);
                return;
            }

            battleOver = false;
            isPlayerTurn = true;

            playerHud?.Bind(playerEntity);
            enemyHud?.Bind(enemyEntity);
            DebugLog("InitializeBattle: HUDs bound.");

            playerActionMenu?.HideMenu();

            if (battleLogText != null)
            {
                battleLogText.text = string.Empty;
                DebugLog("InitializeBattle: Battle log cleared.");
            }

            if (victoryScreen != null)
            {
                victoryScreen.SetActive(false);
            }

            if (victoryMessageText != null)
            {
                victoryMessageText.text = string.Empty;
            }

            if (victoryRewardText != null)
            {
                victoryRewardText.text = string.Empty;
            }

            WriteLog($"{playerEntity.DisplayName} engages {enemyEntity.DisplayName}!");
            DebugLog("InitializeBattle: {0} engages {1}.", playerEntity.DisplayName, enemyEntity.DisplayName);

            EnterPlayerTurn();
        }

        private void ResolveAttack(ICombatEntity attacker, ICombatEntity defender)
        {
            if (attacker == null || defender == null || battleOver)
            {
                DebugLog("ResolveAttack aborted: attacker={0}, defender={1}, battleOver={2}", attacker, defender, battleOver);
                return;
            }

            var attackResult = attacker.AttackStrategy.Execute(attacker, defender);
            defender.ReceiveDamage(attackResult.Damage);
            WriteLog($"{attacker.DisplayName} {attackResult.Description}");
            DebugLog("ResolveAttack: {0} dealt {1} to {2}. HP now {3}/{4}.", attacker.DisplayName, attackResult.Damage, defender.DisplayName, defender.CurrentHp, defender.MaxHp);

            if (!defender.IsAlive)
            {
                HandleDefeat(defender);
            }
        }

        private void BeginEnemyTurn()
        {
            playerActionMenu?.HideMenu();

            if (enemyTurnRoutine != null)
            {
                StopCoroutine(enemyTurnRoutine);
            }

            enemyTurnRoutine = StartCoroutine(EnemyTurnRoutine());
            DebugLog("BeginEnemyTurn: Coroutine started.");
        }

        private IEnumerator EnemyTurnRoutine()
        {
            isPlayerTurn = false;
            DebugLog("EnemyTurnRoutine: Enemy thinking...");

            if (enemyTurnDelay > 0f)
            {
                yield return new WaitForSeconds(enemyTurnDelay);
                DebugLog("EnemyTurnRoutine: Delay {0}s elapsed.", enemyTurnDelay);
            }

            if (battleOver)
            {
                enemyTurnRoutine = null;
                yield break;
            }

            if (enemyAttackAnimator != null)
            {
                bool animationCompleted = false;
                enemyAttackAnimator.PlayAttack(
                    onImpact: () => ResolveAttack(enemyEntity, playerEntity),
                    onComplete: () => animationCompleted = true);

                while (!animationCompleted && !battleOver)
                {
                    yield return null;
                }
            }
            else
            {
                ResolveAttack(enemyEntity, playerEntity);
            }

            if (!battleOver)
            {
                EnterPlayerTurn();
            }

            enemyTurnRoutine = null;
            DebugLog("EnemyTurnRoutine: Coroutine ended.");
        }

        private void HandleDefeat(ICombatEntity defeatedEntity)
        {
            if (battleOver)
            {
                DebugLog("HandleDefeat ignored: Battle already over.");
                return;
            }

            battleOver = true;
            isPlayerTurn = false;

            playerActionMenu?.HideMenu();
            WriteLog($"{defeatedEntity.DisplayName} has been defeated!");
            DebugLog("HandleDefeat: {0} defeated. Battle over.", defeatedEntity.DisplayName);

            ShowVictoryScreen(defeatedEntity);
            UpdateVictoryRewards(defeatedEntity);

            if (destroyDefeatedEntities)
            {
                DestroyEntity(defeatedEntity);
            }
        }

        private void WriteLog(string message)
        {
            if (battleLogText == null)
            {
                DebugLog("WriteLog skipped: battleLogText missing.");
                return;
            }

            battleLogText.text = message;
            DebugLog("WriteLog: {0}", message);
        }

        private static ICombatEntity ExtractCombatEntity(MonoBehaviour behaviour, string label)
        {
            if (behaviour is ICombatEntity entity)
            {
                return entity;
            }

            if (behaviour != null)
            {
                Debug.LogError($"{label} must implement {nameof(ICombatEntity)}.", behaviour);
            }

            return null;
        }

        private void Subscribe(ICombatEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            entity.OnDefeated += HandleDefeat;
            DebugLog("Subscribe: {0}", entity.DisplayName);
        }

        private void Unsubscribe(ICombatEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            entity.OnDefeated -= HandleDefeat;
            DebugLog("Unsubscribe: {0}", entity.DisplayName);
        }

        private void EnterPlayerTurn()
        {
            if (battleOver)
            {
                return;
            }

            isPlayerTurn = true;
            playerActionMenu?.ShowMenu();
            DebugLog("EnterPlayerTurn: Player action menu shown.");
        }

        private void DebugLog(string message, params object[] args)
        {
            if (!enableDebugLogs)
            {
                return;
            }

            if (args != null && args.Length > 0)
            {
                Debug.LogFormat("[BattleManager] " + message, args);
            }
            else
            {
                Debug.Log("[BattleManager] " + message);
            }
        }

        private void ShowVictoryScreen(ICombatEntity defeatedEntity)
        {
            if (victoryScreen == null)
            {
                return;
            }

            string message;
            if (defeatedEntity == enemyEntity)
            {
                message = playerVictoryMessage;
            }
            else if (defeatedEntity == playerEntity)
            {
                message = playerDefeatMessage;
            }
            else
            {
                message = "Battle Finished";
            }

            if (victoryMessageText != null)
            {
                victoryMessageText.text = message;
            }

            victoryScreen.SetActive(true);
            DebugLog("ShowVictoryScreen: '{0}'", message);
        }

        private void UpdateVictoryRewards(ICombatEntity defeatedEntity)
        {
            if (victoryRewardText == null)
            {
                return;
            }

            if (defeatedEntity == enemyEntity && enemyEntity is IRewardProvider rewardProvider)
            {
                var exp = Mathf.Max(0, rewardProvider.ExperienceReward);
                var z = Mathf.Max(0, rewardProvider.ZReward);
                victoryRewardText.text = string.Format(victoryRewardFormat, exp, z);
                DebugLog("UpdateVictoryRewards: Reward text set to EXP={0}, Z={1}.", exp, z);
            }
            else if (defeatedEntity == playerEntity)
            {
                victoryRewardText.text = defeatRewardText;
                DebugLog("UpdateVictoryRewards: Defeat text set.");
            }
            else
            {
                victoryRewardText.text = string.Empty;
            }
        }

        private void DestroyEntity(ICombatEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            if (entity == playerEntity && playerEntitySource != null)
            {
                DebugLog("DestroyEntity: Destroying player entity GameObject.");
                Destroy(playerEntitySource.gameObject);
            }
            else if (entity == enemyEntity && enemyEntitySource != null)
            {
                DebugLog("DestroyEntity: Destroying enemy entity GameObject.");
                Destroy(enemyEntitySource.gameObject);
            }
        }
    }
}
