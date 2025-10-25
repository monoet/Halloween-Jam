using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using BattleV2.Actions;

using BattleV2.Charge;
using BattleV2.Core;
using BattleV2.Execution;
using BattleV2.Execution.TimedHits;
using BattleV2.Orchestration.Services;
using BattleV2.Orchestration.Events;
using BattleV2.Providers;
using BattleV2.Targeting;
using BattleV2.UI;
using HalloweenJam.Combat;

namespace BattleV2.Orchestration
{
    /// <summary>
    /// New orchestration entry point. Coordinates dedicated services instead of
    /// mezclar targeting, turnos y animaciones en una sola clase gigante.
    /// </summary>
    public class BattleManagerV2 : MonoBehaviour
    {
        [Header("Core")]
        [SerializeField] private BattleStateController state;
        [SerializeField] private BattleConfig config;
        [SerializeField] private ActionCatalog actionCatalog;
        [SerializeField] private ScriptableObject inputProviderAsset;
        [SerializeField] private MonoBehaviour inputProviderComponent;
        [SerializeField] private HUDManager hudManager;

        [Header("Entities")]
        [SerializeField] private CombatantState player;
        [SerializeField] private CharacterRuntime playerRuntime;
        [SerializeField] private CombatantState enemy;
        [SerializeField] private CharacterRuntime enemyRuntime;

        [Header("Player Spawn")]
        [SerializeField] private bool autoSpawnPlayer = true;
        [SerializeField] private PlayerLoadout playerLoadout;
        [SerializeField] private Transform playerSpawnPoint;
        [SerializeField] private PlayerPartyLoadout playerPartyLoadout;
        [SerializeField] private Transform[] playerSpawnPoints;

        [Header("Enemy Spawn")]
        [SerializeField] private bool autoSpawnEnemy = true;
        [SerializeField] private EnemyLoadout enemyLoadout;
        [SerializeField] private Transform enemySpawnPoint;
        [SerializeField] private EnemyEncounterLoadout enemyEncounterLoadout;
        [SerializeField] private Transform[] enemySpawnPoints;

        [Header("Pipeline Tuning")]
        [SerializeField, Min(0f)] private float preActionDelaySeconds = 0.12f;

        private IBattleInputProvider inputProvider;
        private CombatContext context;
        private ITimedHitRunner timedHitRunner;

        private ITurnController turnController;
        private ITargetingCoordinator targetingCoordinator;
        private IActionPipeline actionPipeline;
        private ITriggeredEffectsService triggeredEffects;
        private IBattleAnimOrchestrator animOrchestrator;
        private IBattleEventBus eventBus;
        private CombatantRosterService rosterService;
        private RosterSnapshot rosterSnapshot = RosterSnapshot.Empty;

        public event Action<BattleSelection, int> OnPlayerActionSelected;
        public event Action<BattleSelection, int, int> OnPlayerActionResolved;
        public event Action<IReadOnlyList<CombatantState>, IReadOnlyList<CombatantState>> OnCombatantsBound;

        public BattleActionData LastExecutedAction { get; private set; }
        public ITimedHitRunner TimedHitRunner => timedHitRunner ?? InstantTimedHitRunner.Shared;
        public TargetResolverRegistry TargetResolvers { get; private set; }
        public CombatantState Player => player;
        public CombatantState Enemy => enemy;
        public IReadOnlyList<CombatantState> Allies => rosterSnapshot.Allies ?? Array.Empty<CombatantState>();
        public IReadOnlyList<CombatantState> Enemies => rosterSnapshot.Enemies ?? Array.Empty<CombatantState>();
        public float PreActionDelaySeconds
        {
            get => preActionDelaySeconds;
            set => preActionDelaySeconds = Mathf.Max(0f, value);
        }
        public ScriptableObject EnemyDropTable => rosterSnapshot.EnemyDropTable;
        public IReadOnlyList<GameObject> SpawnedPlayerInstances => rosterSnapshot.SpawnedPlayerInstances ?? Array.Empty<GameObject>();
        public IReadOnlyList<GameObject> SpawnedEnemyInstances => rosterSnapshot.SpawnedEnemyInstances ?? Array.Empty<GameObject>();
        public GameObject SpawnedEnemyInstance => rosterSnapshot.SpawnedEnemyInstances.Count > 0 ? rosterSnapshot.SpawnedEnemyInstances[0] : null;
        public GameObject SpawnedPlayerInstance => rosterSnapshot.SpawnedPlayerInstances.Count > 0 ? rosterSnapshot.SpawnedPlayerInstances[0] : null;

        private IReadOnlyList<CombatantState> AlliesList => rosterSnapshot.Allies ?? Array.Empty<CombatantState>();
        private IReadOnlyList<CombatantState> EnemiesList => rosterSnapshot.Enemies ?? Array.Empty<CombatantState>();

        private bool IsAlly(CombatantState combatant)
        {
            if (combatant == null)
            {
                return false;
            }

            var allies = AlliesList;
            for (int i = 0; i < allies.Count; i++)
            {
                if (allies[i] == combatant)
                {
                    return true;
                }
            }

            return false;
        }

        private void Awake()
        {
            BootstrapServices();
            InitializeCombatants();
            PrepareContext();
        }

        private void OnEnable()
        {
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
            rosterService?.Cleanup();
            rosterSnapshot = RosterSnapshot.Empty;
            player = null;
            playerRuntime = null;
            enemy = null;
            enemyRuntime = null;
        }

        private void BootstrapServices()
        {
            TargetResolvers = ShapeResolverBootstrap.RegisterDefaults(new TargetResolverRegistry());

            if (config != null)
            {
                actionCatalog ??= config.actionCatalog;

                if (inputProviderAsset == null && inputProviderComponent == null && config.inputProvider != null)
                {
                    if (config.inputProvider is MonoBehaviour componentProvider)
                    {
                        inputProviderComponent = componentProvider;
                    }
                    else if (config.inputProvider is ScriptableObject assetProvider)
                    {
                        inputProviderAsset = assetProvider;
                    }
                }
            }

            inputProvider = ResolveProvider() ?? ScriptableObject.CreateInstance<AutoBattleInputProvider>();
            timedHitRunner = InstantTimedHitRunner.Shared;

            ComboPointScaling.Configure(config != null ? config.comboPointScaling : null);

            // Servicios dedicados — por ahora implementaciones por defecto mínimas.
            eventBus = new BattleEventBus();
            turnController = new TurnController(eventBus);
            targetingCoordinator = new TargetingCoordinator(TargetResolvers, eventBus);
            actionPipeline = new ActionPipeline(eventBus);
            triggeredEffects = new TriggeredEffectsService(this, actionPipeline, actionCatalog, eventBus);
            animOrchestrator = new BattleAnimOrchestrator(eventBus);
            rosterService = new CombatantRosterService();
        }

        private void PrepareContext()
        {
            var services = config != null ? config.services : new BattleServices();
            context = new CombatContext(
                player,
                enemy,
                playerRuntime,
                enemyRuntime,
                services,
                actionCatalog);
        }

        private void InitializeCombatants()
        {
            RebuildRoster(preservePlayerVitals: false, preserveEnemyVitals: false);
        }

        private void RebuildRoster(bool preservePlayerVitals, bool preserveEnemyVitals)
        {
            if (rosterService == null)
            {
                rosterService = new CombatantRosterService();
            }

            var rosterConfig = new BattleRosterConfig
            {
                AutoSpawnPlayer = autoSpawnPlayer,
                PlayerLoadout = playerLoadout,
                PlayerSpawnPoint = playerSpawnPoint,
                PlayerPartyLoadout = playerPartyLoadout,
                PlayerSpawnPoints = playerSpawnPoints,
                AutoSpawnEnemy = autoSpawnEnemy,
                EnemyLoadout = enemyLoadout,
                EnemySpawnPoint = enemySpawnPoint,
                EnemyEncounterLoadout = enemyEncounterLoadout,
                EnemySpawnPoints = enemySpawnPoints,
                OwnerTransform = transform,
                Player = player,
                PlayerRuntime = playerRuntime,
                Enemy = enemy,
                EnemyRuntime = enemyRuntime,
                HudManager = hudManager
            };

            rosterSnapshot = rosterService.Rebuild(rosterConfig, preservePlayerVitals, preserveEnemyVitals);
            player = rosterSnapshot.Player;
            playerRuntime = rosterSnapshot.PlayerRuntime;
            enemy = rosterSnapshot.Enemy;
            enemyRuntime = rosterSnapshot.EnemyRuntime;

            turnController?.Rebuild(AlliesList, EnemiesList);
            OnCombatantsBound?.Invoke(AlliesList, EnemiesList);
            RefreshCombatContext();
        }

        private void RefreshCombatContext()
        {
            var services = context != null ? context.Services : (config != null ? config.services : new BattleServices());
            context = new CombatContext(
                player,
                enemy,
                playerRuntime,
                enemyRuntime,
                services,
                actionCatalog);
        }

        private void SubscribeEvents()
        {
            animOrchestrator?.StartListening();
        }

        private void UnsubscribeEvents()
        {
            animOrchestrator?.StopListening();
        }

        private CharacterRuntime ResolveRuntime(CombatantState combatant, CharacterRuntime overrideRuntime)
        {
            if (overrideRuntime != null)
            {
                return overrideRuntime;
            }

            if (combatant == null)
            {
                return null;
            }

            return combatant.CharacterRuntime != null
                ? combatant.CharacterRuntime
                : combatant.GetComponent<CharacterRuntime>();
        }

        private IBattleInputProvider ResolveProvider()
        {
            if (inputProviderComponent != null && inputProviderComponent is IBattleInputProvider providerComponent)
            {
                return providerComponent;
            }

            if (inputProviderAsset != null && inputProviderAsset is IBattleInputProvider providerAsset)
            {
                return providerAsset;
            }

            return null;
        }

        public void StartBattle()
        {
            state?.ResetToIdle();
            state?.Set(BattleState.AwaitingAction);
            ContinueBattleLoop();
        }

        public void ResetBattle()
        {
            ComboPointScaling.Configure(config != null ? config.comboPointScaling : null);
            RebuildRoster(preservePlayerVitals: false, preserveEnemyVitals: false);
            PrepareContext();
            state?.ResetToIdle();
            state?.Set(BattleState.AwaitingAction);
            ContinueBattleLoop();
        }

        private void HandlePlayerSelection(BattleSelection selection)
        {
            ProcessPlayerSelection(selection);
        }

        private async void ProcessPlayerSelection(BattleSelection selection)
        {
            if (selection.Action == null || player == null)
            {
                ExecuteFallback();
                return;
            }

            if (!TryResolveAction(selection.Action, out var implementation))
            {
                ExecuteFallback();
                return;
            }

            int totalCpRequired = implementation.CostCP + Mathf.Max(0, selection.CpCharge);
            if (player.CurrentCP < totalCpRequired)
            {
                Debug.LogWarning($"[BattleManagerV2] Not enough CP for {selection.Action.id} (needs {totalCpRequired}, has {player.CurrentCP}).");
                ExecuteFallback();
                return;
            }

            if (!implementation.CanExecute(player, context, selection.CpCharge))
            {
                Debug.LogWarning($"[BattleManagerV2] Action {selection.Action.id} cannot execute.");
                ExecuteFallback();
                return;
            }

            var resolution = await targetingCoordinator.ResolveAsync(
                player,
                selection.Action,
                TargetSourceType.Manual,
                enemy,
                AlliesList,
                EnemiesList);

            if (resolution.Targets.Count == 0)
            {
                Debug.LogWarning($"[BattleManagerV2] Failed to resolve targets for {selection.Action.id}.");
                if (!TryResolveBattleEnd())
                {
                    ExecuteFallback();
                }
                return;
            }

            var enrichedSelection = selection.WithTargets(resolution.TargetSet);

            int cpBefore = player.CurrentCP;
            LastExecutedAction = enrichedSelection.Action;
            OnPlayerActionSelected?.Invoke(enrichedSelection, cpBefore);
            eventBus?.Publish(new ActionStartedEvent(player, enrichedSelection));
            state?.Set(BattleState.Resolving);
            ExecutePlayerAction(enrichedSelection, implementation, cpBefore, resolution.Targets);
        }

        private async void ExecutePlayerAction(BattleSelection selection, IAction implementation, int cpBefore, IReadOnlyList<CombatantState> targets)
        {
            try
            {
                var request = new ActionRequest(
                    this,
                    player,
                    targets,
                    selection,
                    implementation,
                    context);

                var result = await actionPipeline.Run(request);

                if (!result.Success)
                {
                    ExecuteFallback();
                    return;
                }

                var effectSelection = new BattleSelection(
                    selection.Action,
                    0,
                    selection.ChargeProfile,
                    selection.TimedHitProfile,
                    null,
                    selection.Targets);

                triggeredEffects.Enqueue(new TriggeredEffectRequest(
                    player,
                    selection.Action,
                    effectSelection,
                    targets,
                    context));
                RefreshCombatContext();

                int cpAfter = player != null ? player.CurrentCP : 0;
                var resolvedSelection = selection.WithTimedResult(result.TimedResult);

                OnPlayerActionResolved?.Invoke(resolvedSelection, cpBefore, cpAfter);
                eventBus?.Publish(new ActionCompletedEvent(player, resolvedSelection));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BattleManagerV2] Player action threw exception: {ex}");
            }
            finally
            {
                turnController?.Rebuild(AlliesList, EnemiesList);
                turnController?.Next();
                if (!TryResolveBattleEnd())
                {
                    RefreshCombatContext();
                    state?.Set(BattleState.AwaitingAction);
                    ContinueBattleLoop();
                }
            }
        }

        private void ExecuteFallback()
        {
            if (player == null || actionCatalog == null)
            {
                return;
            }

            var fallback = actionCatalog.Fallback(player, context);
            if (fallback == null)
            {
                Debug.LogWarning("[BattleManagerV2] Fallback action is null.");
                return;
            }

            if (!TryResolveAction(fallback, out var implementation))
            {
                Debug.LogWarning($"[BattleManagerV2] Fallback {fallback.id} has no implementation.");
                return;
            }

            HandlePlayerSelection(new BattleSelection(fallback, 0, implementation.ChargeProfile, null));
        }

        private void ExecuteEnemyTurn(CombatantState attacker)
        {
            if (attacker == null || !attacker.IsAlive)
            {
                AdvanceAfterEnemyTurn();
                return;
            }

            state?.Set(BattleState.Resolving);

            var target = player;
            if (target == null || target.IsDead())
            {
                state?.Set(BattleState.Defeat);
                return;
            }

            var services = context != null ? context.Services : (config != null ? config.services : new BattleServices());
            var enemyContext = new CombatContext(
                attacker,
                target,
                ResolveRuntime(attacker, attacker?.CharacterRuntime),
                ResolveRuntime(target, target?.CharacterRuntime ?? playerRuntime),
                services,
                actionCatalog);

            var available = actionCatalog?.BuildAvailableFor(attacker, enemyContext);
            if (available == null || available.Count == 0)
            {
                Debug.LogWarning($"[BattleManagerV2] Enemy {attacker.name} has no actions.");
                AdvanceAfterEnemyTurn();
                return;
            }

            var actionData = available[0];
            if (!TryResolveAction(actionData, out var implementation))
            {
                Debug.LogWarning($"[BattleManagerV2] Enemy action {actionData.id} missing implementation.");
                AdvanceAfterEnemyTurn();
                return;
            }

            if (implementation.CostSP > 0 && !attacker.SpendSP(implementation.CostSP))
            {
                Debug.LogWarning($"[BattleManagerV2] Enemy lacks SP for {actionData.id}.");
                AdvanceAfterEnemyTurn();
                return;
            }

            if (implementation.CostCP > 0 && !attacker.SpendCP(implementation.CostCP))
            {
                Debug.LogWarning($"[BattleManagerV2] Enemy lacks CP for {actionData.id}.");
                AdvanceAfterEnemyTurn();
                return;
            }

            var selection = new BattleSelection(
                actionData,
                0,
                implementation.ChargeProfile,
                null);

            RunEnemyAction(attacker, target, selection, implementation, enemyContext);
        }

        private async void RunEnemyAction(
            CombatantState attacker,
            CombatantState target,
            BattleSelection selection,
            IAction implementation,
            CombatContext enemyContext)
        {
            try
            {
                var resolution = await targetingCoordinator.ResolveAsync(
                    attacker,
                    selection.Action,
                    TargetSourceType.Auto,
                    target,
                    AlliesList,
                    EnemiesList);

                if (resolution.Targets.Count == 0)
                {
                    AdvanceAfterEnemyTurn();
                    return;
                }

                var enrichedSelection = selection.WithTargets(resolution.TargetSet);

                var request = new ActionRequest(
                    this,
                    attacker,
                    resolution.Targets,
                    enrichedSelection,
                    implementation,
                    enemyContext);

                var result = await actionPipeline.Run(request);
                if (result.Success)
                {
                    var effectSelection = new BattleSelection(
                        enrichedSelection.Action,
                        0,
                        enrichedSelection.ChargeProfile,
                        enrichedSelection.TimedHitProfile,
                        null,
                        enrichedSelection.Targets);

                    triggeredEffects.Enqueue(new TriggeredEffectRequest(
                        attacker,
                        enrichedSelection.Action,
                        effectSelection,
                        resolution.Targets,
                        enemyContext));
                }

                eventBus?.Publish(new ActionCompletedEvent(attacker, enrichedSelection.WithTimedResult(result.TimedResult)));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BattleManagerV2] Enemy action error: {ex}");
            }
            finally
            {
                RefreshCombatContext();
                AdvanceAfterEnemyTurn();
            }
        }

        private void AdvanceAfterEnemyTurn()
        {
            RefreshCombatContext();
            turnController?.Rebuild(AlliesList, EnemiesList);
            turnController?.Next();
            if (!TryResolveBattleEnd())
            {
                state?.Set(BattleState.AwaitingAction);
                ContinueBattleLoop();
            }
        }

        private bool TryResolveBattleEnd()
        {
            if (player == null || player.IsDead())
            {
                state?.Set(BattleState.Defeat);
                return true;
            }

            var enemies = EnemiesList;
            for (int i = 0; i < enemies.Count; i++)
            {
                var combatant = enemies[i];
                if (combatant != null && combatant.IsAlive)
                {
                    return false;
                }
            }

            state?.Set(BattleState.Victory);
            return true;
        }

        private void ContinueBattleLoop()
        {
            if (state == null || state.State != BattleState.AwaitingAction)
            {
                return;
            }

            if (TryResolveBattleEnd())
            {
                return;
            }

            var current = turnController?.Current ?? player;
            if (current == null)
            {
                state?.Set(BattleState.Defeat);
                return;
            }

             if (!current.IsAlive)
            {
                turnController?.Next();
                ContinueBattleLoop();
                return;
            }

            if (IsAlly(current))
            {
                RequestPlayerAction();
            }
            else
            {
                ExecuteEnemyTurn(current);
            }
        }

        private void RequestPlayerAction()
        {
            if (player == null || player.IsDead())
            {
                state?.Set(BattleState.Defeat);
                return;
            }

            var available = actionCatalog?.BuildAvailableFor(player, context);
            if (available == null || available.Count == 0)
            {
                ExecuteFallback();
                return;
            }

            var actionContext = new BattleActionContext
            {
                Player = player,
                PlayerRuntime = playerRuntime ?? player?.CharacterRuntime,
                Enemy = enemy,
                EnemyRuntime = enemyRuntime ?? enemy?.CharacterRuntime,
                AvailableActions = available,
                Context = context,
                MaxCpCharge = player != null ? player.CurrentCP : 0,
                PlayerStats = player != null ? player.FinalStats : default,
                EnemyStats = enemy != null ? enemy.FinalStats : default
            };

            inputProvider?.RequestAction(actionContext, HandlePlayerSelection, ExecuteFallback);
        }

        private bool TryResolveAction(BattleActionData action, out IAction implementation)
        {
            implementation = null;

            if (actionCatalog == null || action == null)
            {
                return false;
            }

            implementation = actionCatalog.Resolve(action);
            return implementation != null;
        }
    }
}
