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
                DebugLog,
                AutoOpenPlayerTurn);

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
            uiController?.Detach();
            outcomeController?.Dispose();
            orchestrator?.Dispose();
            actionSelectionUI?.Hide();
        }

        private void OnDestroy()
        {
            if (outcomeController != null)
            {
                outcomeController.BattleFinished -= OnBattleFinished;
            }
        }

        public void OnAttackButton()
        {
            if (orchestrator == null)
            {
                DebugLog("OnAttackButton ignored: orchestrator missing.");
                return;
            }

            if (!orchestrator.CanPlayerAct || orchestrator.IsBusy)
            {
                DebugLog("OnAttackButton ignored: CanPlayerAct={0}, IsBusy={1}", orchestrator.CanPlayerAct, orchestrator.IsBusy);
                return;
            }

            DebugLog("OnAttackButton: Player initiated attack.");

            if (TryHandlePlayerActionSelection())
            {
                return;
            }

            if (!TryQueueAndExecuteFallbackAction("OnAttackButton fallback"))
            {
                DebugLog("OnAttackButton: No available actions to queue and selector unavailable. Turn skipped.");
            }
        }

        private void AutoOpenPlayerTurn()
        {
            if (orchestrator == null)
            {
                return;
            }

            if (!orchestrator.CanPlayerAct || orchestrator.IsBusy)
            {
                return;
            }

            DebugLog("AutoOpenPlayerTurn: attempting to start player turn (bypass UI).");
            if (!TryQueueAndExecuteFallbackAction("AutoOpenPlayerTurn"))
            {
                DebugLog("AutoOpenPlayerTurn: fallback failed (no actions?).");
            }
        }

        private bool TryQueueAndExecuteFallbackAction(string label)
        {
            if (orchestrator == null)
            {
                return false;
            }

            if (orchestrator.IsBusy)
            {
                DebugLog("{0}: orchestrator busy, skipping fallback.", label);
                return false;
            }

            if (playerEntity is RuntimeCombatEntity runtime && runtime.AvailableActions != null && runtime.AvailableActions.Count > 0)
            {
                var fallbackAction = runtime.AvailableActions[0];
                if (fallbackAction == null)
                {
                    DebugLog("{0}: fallback action is null.", label);
                    return false;
                }

                runtime.QueueAction(fallbackAction);
                playerActionMenu?.HideMenu();

                if (!orchestrator.IsBusy)
                {
                    DebugLog("{0}: executing fallback action {1}.", label, fallbackAction.name);
                    orchestrator.ExecutePlayerTurn();
                    return true;
                }
                else
                {
                    DebugLog("{0}: orchestrator became busy before execute.", label);
                }
            }
            else
            {
                DebugLog("{0}: runtime has no available actions.", label);
            }

            return false;
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

        private bool TryHandlePlayerActionSelection()
        {
            if (actionSelectionUI == null)
            {
                return false;
            }

            if (!actionSelectionUI.CanShow)
            {
                DebugLog("TryHandlePlayerActionSelection: UI currently busy; skipping fallback.");
                return true;
            }

            if (playerEntity is not RuntimeCombatEntity runtime)
            {
                return false;
            }

            var actions = runtime.AvailableActions;
            if (actions == null || actions.Count == 0)
            {
                return false;
            }

            playerActionMenu?.HideMenu();

            actionSelectionUI.SetInteractionGuard(() => orchestrator != null && orchestrator.IsBusy);

            actionSelectionUI.Show(runtime, selected =>
            {
                if (selected == null)
                {
                    DebugLog("OnAttackButton: Selection UI returned null action. Turn cancelled.");
                    return;
                }

                DebugLog("Player action selected: {0}", selected.name);
                runtime.QueueAction(selected);
                if (!orchestrator.IsBusy)
                {
                    orchestrator.ExecutePlayerTurn();
                }
                else
                {
                    DebugLog("OnAttackButton: Execute skipped because orchestrator is busy.");
                }
            });

            return true;
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
    }
}

