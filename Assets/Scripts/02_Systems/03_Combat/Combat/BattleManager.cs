using System;
using HalloweenJam.Combat.Animations;
using HalloweenJam.Combat.Strategies;
using HalloweenJam.UI.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace HalloweenJam.Combat
{
    /// <summary>
    /// Entry point that wires combat services together and exposes battle lifecycle events.
    /// </summary>
    public class BattleManager : MonoBehaviour
    {
        [Header("Bindings")]
        [SerializeField] private MonoBehaviour playerEntitySource;
        [SerializeField] private MonoBehaviour enemyEntitySource;
        [SerializeField] private BattleHUD playerHud;
        [SerializeField] private BattleHUD enemyHud;
        [SerializeField] private TMP_Text battleLogText;
        [SerializeField] private BattleActionMenu playerActionMenu;
        [SerializeField] private ActionSelectionUI actionSelectionUI;
        [SerializeField, FormerlySerializedAs("playerAttackAnimator")] private MonoBehaviour playerAttackAnimatorSource;
        [SerializeField, FormerlySerializedAs("enemyAttackAnimator")] private MonoBehaviour enemyAttackAnimatorSource;
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
        [SerializeField] private BattleTurnStrategyBase playerTurnStrategy;
        [SerializeField] private BattleTurnStrategyBase enemyTurnStrategy;

        private ICombatEntity playerEntity;
        private ICombatEntity enemyEntity;
        private IAttackAnimator playerAttackAnimator;
        private IAttackAnimator enemyAttackAnimator;
        private BattleUIController uiController;
        private BattleMusicController musicController;
        private BattleActionResolver actionResolver;
        private BattleOrchestrator orchestrator;
        private BattleOutcomeController outcomeController;
        private BattleTurnStrategyBase playerTurnStrategyInstance;
        private BattleTurnStrategyBase enemyTurnStrategyInstance;
        private BattleOutcome? lastOutcome;

        public event Action<BattleOutcome> BattleCompleted;

        public bool IsBattleActive => orchestrator != null && !orchestrator.BattleOver;
        public BattleOutcome? LastOutcome => lastOutcome;

        private void Awake()
        {
            playerEntity = ExtractCombatEntity(playerEntitySource, "Player Entity");
            enemyEntity = ExtractCombatEntity(enemyEntitySource, "Enemy Entity");
            playerAttackAnimator = ExtractAttackAnimator(playerAttackAnimatorSource, "Player Attack Animator");
            enemyAttackAnimator = ExtractAttackAnimator(enemyAttackAnimatorSource, "Enemy Attack Animator");

            playerTurnStrategyInstance = EnsureStrategyInstance(playerTurnStrategy);
            enemyTurnStrategyInstance = EnsureStrategyInstance(enemyTurnStrategy);

            actionResolver = new BattleActionResolver();
            uiController = new BattleUIController(
                playerHud,
                enemyHud,
                battleLogText,
                playerActionMenu,
                victoryScreen,
                victoryMessageText,
                victoryRewardText,
                victoryRewardFormat,
                defeatRewardText,
                playerVictoryMessage,
                playerDefeatMessage,
                DebugLog);

            musicController = new BattleMusicController(musicSource, battleMusicClip, victoryMusicClip);

            orchestrator = new BattleOrchestrator(
                this,
                actionResolver,
                playerTurnStrategyInstance,
                enemyTurnStrategyInstance,
                playerAttackAnimator,
                enemyAttackAnimator,
                enemyTurnDelay);

            outcomeController = new BattleOutcomeController(
                uiController,
                musicController,
                destroyDefeatedEntities,
                playerEntitySource,
                enemyEntitySource,
                DebugLog);
            outcomeController.BattleFinished += OnBattleFinished;

            DebugLog("Awake: Player entity set? {0} | Enemy entity set? {1}", playerEntity != null, enemyEntity != null);
        }

        private void OnEnable()
        {
            playerActionMenu?.RegisterAttackCallback(OnAttackButton);
        }

        private void Start()
        {
            if (playerEntity == null || enemyEntity == null)
            {
                Debug.LogError("BattleManager cannot initialize without combat entities.", this);
                return;
            }

            InitializeBattle();
        }

        private void OnDisable()
        {
            playerActionMenu?.ClearAttackCallbacks();

            if (uiController != null)
            {
                try
                {
                    uiController.Detach();
                }
                catch (MissingReferenceException)
                {
                    // Ignorar: la UI ya fue destruida
                }
            }

            outcomeController?.Dispose();
            orchestrator?.Dispose();
        }

        public void OnAttackButton()
        {
            if (orchestrator == null || !orchestrator.CanPlayerAct)
            {
                DebugLog("OnAttackButton ignored: orchestrator ready? {0}", orchestrator != null);
                return;
            }

            var selector = ResolveActionSelector();
            if (selector != null)
            {
                var runtimePlayer = playerEntity as RuntimeCombatEntity;
                if (runtimePlayer == null)
                {
                    DebugLog("OnAttackButton: Player entity is not RuntimeCombatEntity, using default action.");
                    orchestrator.ExecutePlayerTurn();
                    return;
                }

        bool selectorInsideActionMenu = false;
        if (playerActionMenu != null)
        {
            var selectorRoot = selector.RootTransform;
            var selectorContainer = selector.ButtonContainer;

            if (selectorRoot != null && selectorRoot.IsChildOf(playerActionMenu.transform))
            {
                selectorInsideActionMenu = true;
            }
            else if (selectorContainer != null && selectorContainer.IsChildOf(playerActionMenu.transform))
            {
                selectorInsideActionMenu = true;
            }
        }

        DebugLog("OnAttackButton: selectorInsideActionMenu={0}", selectorInsideActionMenu);
        bool? previousAutoHide = null;

        if (playerActionMenu != null)
        {
            previousAutoHide = playerActionMenu.AutoHideOnConfirm;

            if (selectorInsideActionMenu)
            {
                playerActionMenu.AutoHideOnConfirm = false;
            }
            else if (previousAutoHide.Value)
            {
                // When the selector lives outside the action menu we can safely hide it right away.
                playerActionMenu.HideMenu();
            }
        }

        void RestoreMenuState()
        {
            if (playerActionMenu == null || !previousAutoHide.HasValue)
            {
                return;
            }

            playerActionMenu.AutoHideOnConfirm = previousAutoHide.Value;

            if (selectorInsideActionMenu && previousAutoHide.Value)
            {
                playerActionMenu.HideMenu();
            }
        }

                DebugLog("OnAttackButton: Opening ActionSelectionUI for player.");
                try
                {
                    selector.Show(runtimePlayer, selectedAction =>
                    {
                        DebugLog("Action selected: {0}", selectedAction != null ? selectedAction.name : "null");

                        if (selectedAction != null)
                {
                    runtimePlayer.QueueAction(selectedAction);
                }

                orchestrator.ExecutePlayerTurn();
                RestoreMenuState();
            });
        }
        catch
        {
            RestoreMenuState();
            throw;
        }

                return;
            }

            DebugLog("OnAttackButton: No ActionSelectionUI assigned, using default action.");
            orchestrator.ExecutePlayerTurn();
        }
        private void InitializeBattle()
        {
            uiController.Initialize(playerEntity, enemyEntity);
            uiController.Attach(orchestrator, actionResolver);

            outcomeController.Initialize(orchestrator, playerEntity, enemyEntity);

            orchestrator.Initialize(playerEntity, enemyEntity);

            lastOutcome = null;

            uiController.ShowEngagementMessage();
            musicController?.PlayBattleMusic();
        }

        private void OnBattleFinished(BattleOutcome outcome)
        {
            lastOutcome = outcome;
            DebugLog("Battle finished with result {0}.", outcome.Victory);
            BattleCompleted?.Invoke(outcome);
        }

        private BattleTurnStrategyBase EnsureStrategyInstance(BattleTurnStrategyBase configured)
        {
            if (configured != null)
            {
                return configured;
            }

            var instance = ScriptableObject.CreateInstance<DefaultBattleTurnStrategy>();
            instance.hideFlags = HideFlags.HideAndDontSave;
            return instance;
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

        private static IAttackAnimator ExtractAttackAnimator(MonoBehaviour behaviour, string label)
        {
            if (behaviour is IAttackAnimator animator)
            {
                return animator;
            }

            if (behaviour != null)
            {
                Debug.LogError($"{label} must implement {nameof(IAttackAnimator)}.", behaviour);
            }

            return null;
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

        private ActionSelectionUI ResolveActionSelector()
        {
            if (actionSelectionUI != null)
            {
                return actionSelectionUI;
            }

            if (playerActionMenu != null)
            {
                actionSelectionUI = playerActionMenu.GetComponentInChildren<ActionSelectionUI>(true);
                if (actionSelectionUI != null)
                {
                    return actionSelectionUI;
                }
            }

            actionSelectionUI = GetComponentInChildren<ActionSelectionUI>(true);
            return actionSelectionUI;
        }
    }
}


