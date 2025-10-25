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
using BattleV2.Orchestration.Services.Animation;
using BattleV2.Orchestration.Services.End;
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

        [Header("Timing")]
        [SerializeField] private BattleTimingConfig timingConfig;

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

        private IBattleTurnService turnService;
        private ICombatantActionValidator actionValidator;
        private IBattleEndService battleEndService;
        private ITargetingCoordinator targetingCoordinator;
        private IActionPipeline actionPipeline;
        private ITriggeredEffectsService triggeredEffects;
        private IBattleAnimOrchestrator animOrchestrator;
        private IBattleEventBus eventBus;
        private IEnemyTurnCoordinator enemyTurnCoordinator;
        private IFallbackActionResolver fallbackActionResolver;
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

        private void OnDisable()
        {
            turnService?.Stop();
            triggeredEffects?.Clear();
            rosterService?.Cleanup();
            rosterSnapshot = RosterSnapshot.Empty;
            player = null;
            playerRuntime = null;
            enemy = null;
            enemyRuntime = null;
        }

        private void OnDestroy()
        {
            if (turnService != null)
            {
                turnService.OnTurnReady -= HandleTurnReady;
                if (turnService is IDisposable disposableTurn)
                {
                    disposableTurn.Dispose();
                }

                turnService = null;
            }

            if (battleEndService != null)
            {
                battleEndService.OnBattleEnded -= HandleBattleEnded;
                battleEndService = null;
            }

            triggeredEffects?.Clear();
            triggeredEffects = null;
            enemyTurnCoordinator = null;
            fallbackActionResolver = null;
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
            targetingCoordinator = new TargetingCoordinator(TargetResolvers, eventBus);
            actionPipeline = new ActionPipeline(eventBus);

            var timingProfile = timingConfig != null
                ? timingConfig.ToProfile()
                : (config?.timingConfig != null ? config.timingConfig.ToProfile() : BattleTimingProfile.Default);

            animOrchestrator = new BattleAnimOrchestrator(eventBus, timingProfile);
            triggeredEffects = new TriggeredEffectsService(this, actionPipeline, actionCatalog, eventBus);
            actionValidator = new CombatantActionValidator(actionCatalog);
            fallbackActionResolver = new FallbackActionResolver(actionCatalog, actionValidator);
            enemyTurnCoordinator = new EnemyTurnCoordinator(
                actionCatalog,
                actionValidator,
                targetingCoordinator,
                actionPipeline,
                triggeredEffects,
                animOrchestrator,
                eventBus);
            turnService = new BattleTurnService(eventBus);
            turnService.OnTurnReady += HandleTurnReady;
            battleEndService = new BattleEndService(eventBus);
            battleEndService.OnBattleEnded += HandleBattleEnded;
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

            turnService?.UpdateRoster(rosterSnapshot);
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
            turnService?.Begin();
        }

        public void ResetBattle()
        {
            ComboPointScaling.Configure(config != null ? config.comboPointScaling : null);
            triggeredEffects?.Clear();
            RebuildRoster(preservePlayerVitals: false, preserveEnemyVitals: false);
            PrepareContext();
            state?.ResetToIdle();
            state?.Set(BattleState.AwaitingAction);
            turnService?.Stop();
            turnService?.Begin();
        }

        private void HandlePlayerSelection(BattleSelection selection)
        {
            ProcessPlayerSelection(selection);
        }

        private async void ProcessPlayerSelection(BattleSelection selection)
        {
            if (!actionValidator.TryValidate(selection.Action, player, context, selection.CpCharge, out var implementation))
            {
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

            var playbackTask = animOrchestrator != null
                ? animOrchestrator.PlayAsync(new ActionPlaybackRequest(player, enrichedSelection, resolution.Targets, CalculateAverageSpeed()))
                : Task.CompletedTask;

            state?.Set(BattleState.Resolving);
            ExecutePlayerAction(enrichedSelection, implementation, cpBefore, resolution.Targets, playbackTask);
        }

        private async void ExecutePlayerAction(
            BattleSelection selection,
            IAction implementation,
            int cpBefore,
            IReadOnlyList<CombatantState> targets,
            Task playbackTask)
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

                if (playbackTask != null)
                {
                    try
                    {
                        await playbackTask;
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogError($"[BattleManagerV2] Animation playback failed: {ex}");
                    }
                }

                int cpAfter = player != null ? player.CurrentCP : 0;
                var resolvedSelection = selection.WithTimedResult(result.TimedResult);

                OnPlayerActionResolved?.Invoke(resolvedSelection, cpBefore, cpAfter);

                bool battleEnded = TryResolveBattleEnd();
                eventBus?.Publish(new ActionCompletedEvent(player, resolvedSelection, targets));

                if (battleEnded)
                {
                    return;
                }

                state?.Set(BattleState.AwaitingAction);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BattleManagerV2] Player action threw exception: {ex}");
                ExecuteFallback();
            }
        }

        private void ExecuteFallback()
        {
            if (fallbackActionResolver == null || player == null)
            {
                return;
            }

            if (fallbackActionResolver.TryResolve(player, context, out var selection))
            {
                HandlePlayerSelection(selection);
            }
        }

        private bool TryResolveBattleEnd()
        {
            if (battleEndService == null)
            {
                return false;
            }

            if (battleEndService.TryResolve(rosterSnapshot, player, state))
            {
                turnService?.Stop();
                return true;
            }

            return false;
        }

        private void HandleBattleEnded(BattleResult result)
        {
            triggeredEffects?.Clear();
            turnService?.Stop();
        }

        private async void HandleTurnReady(CombatantState actor)
        {
            if (actor == null)
            {
                TryResolveBattleEnd();
                return;
            }

            if (TryResolveBattleEnd())
            {
                return;
            }

            if (IsAlly(actor))
            {
                state?.Set(BattleState.AwaitingAction);
                RequestPlayerAction();
            }
            else
            {
                if (enemyTurnCoordinator != null)
                {
                    var enemyContext = BuildEnemyTurnContext(actor);
                    await enemyTurnCoordinator.ExecuteAsync(enemyContext);
                }
            }
        }

        private float CalculateAverageSpeed()
        {
            float total = 0f;
            int count = 0;

            Accumulate(AlliesList);
            Accumulate(EnemiesList);

            return count > 0 ? total / count : 1f;

            void Accumulate(IReadOnlyList<CombatantState> list)
            {
                if (list == null)
                {
                    return;
                }

                for (int i = 0; i < list.Count; i++)
                {
                    var combatant = list[i];
                    if (combatant == null || !combatant.IsAlive)
                    {
                        continue;
                    }

                    total += combatant.FinalStats.Speed;
                    count++;
                }
            }
        }

        private void RequestPlayerAction()
        {
            state?.Set(BattleState.AwaitingAction);
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

        private EnemyTurnContext BuildEnemyTurnContext(CombatantState attacker)
        {
            var target = player;
            var combatContext = CreateEnemyCombatContext(attacker, target);

            return new EnemyTurnContext(
                this,
                attacker,
                target,
                combatContext,
                AlliesList,
                EnemiesList,
                CalculateAverageSpeed(),
                state,
                a => turnService?.Advance(a),
                () => turnService?.Stop(),
                TryResolveBattleEnd,
                RefreshCombatContext);
        }

        private CombatContext CreateEnemyCombatContext(CombatantState attacker, CombatantState target)
        {
            var services = context != null ? context.Services : (config != null ? config.services : new BattleServices());
            return new CombatContext(
                attacker,
                target,
                ResolveRuntime(attacker, attacker?.CharacterRuntime),
                ResolveRuntime(target, target?.CharacterRuntime ?? playerRuntime),
                services,
                actionCatalog);
        }
    }
}











