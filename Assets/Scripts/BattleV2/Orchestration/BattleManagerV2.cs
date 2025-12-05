using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using BattleV2.Actions;
using BattleV2.AnimationSystem;
using BattleV2.AnimationSystem.Execution.Runtime;
using BattleV2.AnimationSystem.Runtime;
using BattleV2.Charge;
using BattleV2.Core;
using BattleV2.Execution;
using BattleV2.Execution.TimedHits;
using BattleV2.Core.Services;
using BattleV2.Orchestration.Services;
using BattleV2.Orchestration.Services.Animation;
using BattleV2.Orchestration.Events;
using BattleV2.Providers;
using BattleV2.Targeting;
using BattleV2.UI;
using HalloweenJam.Combat;
using BattleV2.Orchestration.Runtime;

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

        [Header("Animation System")]
        [SerializeField] private AnimationSystemInstaller animationSystemInstaller;
        [SerializeField] private bool useAnimationSystemInstaller = false;
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


        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = false;

        private IBattleInputProvider inputProvider;
        private CombatContext context;
        private TimedHitInputRelay timedHitInputRelay;
        private BattleUIInputDriver inputDriver;

        private IBattleTurnService turnService;
        private ICombatantActionValidator actionValidator;
        private IBattleEndService battleEndService;
        private ITargetingCoordinator targetingCoordinator;
        private IActionPipeline actionPipeline;
        private ITriggeredEffectsService triggeredEffects;
        private ICombatSideService sideService;
        private readonly RuntimeCPIntent cpIntent = RuntimeCPIntent.Shared;
        private IBattleAnimOrchestrator animOrchestrator;
        private IBattleEventBus eventBus;
        private IEnemyTurnCoordinator enemyTurnCoordinator;
        private IFallbackActionResolver fallbackActionResolver;
        private ITimedHitResultResolver timedResultResolver;
        private PlayerActionExecutor playerActionExecutor;
        private CancellationTokenSource battleCts;
        private TargetResolutionService targetResolutionService;
        private CombatContextService contextService;
        private CombatantReferenceService referenceService;
        private CombatantRosterCoordinator rosterCoordinator;
        private RosterSnapshot rosterSnapshot = RosterSnapshot.Empty;
        private PendingPlayerRequest pendingPlayerRequest;
        private IDisposable combatantDefeatedSubscription;
        private int cpSelectionCounter;

        public event Action<BattleSelection, int> OnPlayerActionSelected;
        public event Action<BattleSelection, int, int> OnPlayerActionResolved;
        public event Action<IReadOnlyList<CombatantState>, IReadOnlyList<CombatantState>> OnCombatantsBound;

        public BattleActionData LastExecutedAction { get; private set; }
        public ITimedHitService TimedHitService => animationSystemInstaller != null ? animationSystemInstaller.TimedHitService : null;
        public TargetResolverRegistry TargetResolvers { get; private set; }
        public IReadOnlyList<CombatantState> ActiveAllies => rosterSnapshot.ActiveAllies ?? Array.Empty<CombatantState>();
        public CombatantState PrimaryPlayer => ActiveAllies.Count > 0 ? ActiveAllies[0] : null;
        public CombatantState Player => referenceService?.Player ?? PrimaryPlayer ?? player;
        public CombatantState Enemy => referenceService?.Enemy ?? enemy;
        public ICpIntentSource CpIntentSource => cpIntent;
        public ICpIntentSink CpIntentSink => cpIntent;
        public IReadOnlyList<CombatantState> Allies => ActiveAllies;
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

        private IReadOnlyList<CombatantState> AlliesList => ActiveAllies;
        private IReadOnlyList<CombatantState> EnemiesList => rosterSnapshot.Enemies ?? Array.Empty<CombatantState>();
        private TimedHitOverlay timedHitOverlay;

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
            ResetBattleCts();
            BootstrapServices();
            InitializeCombatants();
            PrepareContext();
            timedHitInputRelay ??= FindTimedHitInputRelay();
            inputDriver = FindObjectOfType<BattleUIInputDriver>();
            timedHitOverlay = FindObjectOfType<TimedHitOverlay>();
        }

        private void Start()
        {
            TrySwitchToAnimationInstaller();
            // Wire CP intent to combat dispatcher once installer is resolved.
            cpIntent.SetDispatcher(animationSystemInstaller?.CombatEvents ?? AnimationSystemInstaller.Current?.CombatEvents);
            cpIntent.SetDefaultActor(Player);
            if (inputDriver != null && TimedHitService != null)
            {
                inputDriver.Initialize(TimedHitService);
            }
        }

        private void OnDisable()
        {
            CancelBattleCts();
            turnService?.Stop();
            triggeredEffects?.Clear();
            ClearPendingPlayerRequest();
            DestroySpawnedPrefabs();
            rosterCoordinator?.Cleanup();
            rosterSnapshot = RosterSnapshot.Empty;
            UpdatePlayerReference(null, null);
            UpdateEnemyReference(null, null);
            referenceService?.Reset();
        }

        private void OnDestroy()
        {
            CancelBattleCts();
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
            ClearPendingPlayerRequest();
            triggeredEffects = null;
            enemyTurnCoordinator = null;
            fallbackActionResolver = null;
            playerActionExecutor = null;
            targetResolutionService = null;
            contextService = null;
            rosterCoordinator = null;
            combatantDefeatedSubscription?.Dispose();
            combatantDefeatedSubscription = null;
        }

        private void BootstrapServices()
        {
            sideService = new CombatSideService();
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

            inputProvider = ResolveProvider();
            if (inputProvider == null)
            {
                Debug.LogWarning("[BattleManagerV2] No input provider configured. Awaiting runtime provider assignment.", this);
            }

            ComboPointScaling.Configure(config != null ? config.comboPointScaling : null);

            // Servicios dedicados — por ahora implementaciones por defecto mínimas.
            eventBus = new BattleEventBus();
            if (timedHitOverlay != null)
            {
                if (useAnimationSystemInstaller && AnimationSystemInstaller.Current != null)
                {
                    timedHitOverlay.Initialize(AnimationSystemInstaller.Current.EventBus);
                }
                else
                {
                    timedHitOverlay.Initialize((IAnimationEventBus)eventBus);
                }
            }
            targetingCoordinator = new TargetingCoordinator(TargetResolvers, eventBus, null, new BattleV2.Targeting.Policies.BackAwareResolutionPolicy(), sideService);
            
            // Fail Fast / Auto-Wire: Ensure we have an interactor for manual targeting
            // Search for ACTIVE first
            var interactor = FindObjectOfType<BattleUITargetInteractor>();
            
            // If not found, search for INACTIVE to give a better error
            if (interactor == null)
            {
                var allInteractors = Resources.FindObjectsOfTypeAll<BattleUITargetInteractor>();
                if (allInteractors != null && allInteractors.Length > 0)
                {
                    // Filter out assets (prefabs) if possible, but Resources.FindObjectsOfTypeAll returns everything.
                    // Better to use FindObjectsOfType(true) in newer Unity, or just warn.
                    // Assuming Unity 2020+:
                    interactor = FindObjectOfType<BattleUITargetInteractor>(true);
                    if (interactor != null)
                    {
                        Debug.LogError($"[BattleManagerV2] CRITICAL: Found BattleUITargetInteractor on '{interactor.name}', but the GameObject is DISABLED. It MUST be active to receive events and initialize! Please enable it or move the script to an active object.");
                        // We cannot use it safely if it's inactive because Awake() hasn't run.
                        interactor = null; 
                    }
                }
            }

            if (interactor != null)
            {
                targetingCoordinator.SetInteractor(interactor);
            }
            else
            {
                Debug.LogWarning("[BattleManagerV2] No BattleUITargetInteractor found. Manual targeting will fall back to legacy/highlight behavior.");
            }

            actionPipeline = new OrchestrationActionPipeline(eventBus);
            targetResolutionService = new TargetResolutionService(targetingCoordinator);
            contextService = new CombatContextService(config, actionCatalog);
            rosterCoordinator = new CombatantRosterCoordinator(new CombatantRosterService(), contextService);
            referenceService = new CombatantReferenceService(player, playerRuntime, enemy, enemyRuntime);
            UpdatePlayerReference(player, playerRuntime);
            UpdateEnemyReference(enemy, enemyRuntime);

            var timingProfile = timingConfig != null
                ? timingConfig.ToProfile()
                : (config?.timingConfig != null ? config.timingConfig.ToProfile() : BattleTimingProfile.Default);

            ConfigureAnimationOrchestrator(timingProfile);
            triggeredEffects = new TriggeredEffectsService(this, actionPipeline, actionCatalog, eventBus);
            timedResultResolver = new TimedHitResultResolver();
            RebuildPlayerActionExecutor();
            actionValidator = new CombatantActionValidator(actionCatalog);
            fallbackActionResolver = new FallbackActionResolver(actionCatalog, actionValidator);
            enemyTurnCoordinator = new EnemyTurnCoordinator(
                actionCatalog,
                actionValidator,
                targetingCoordinator,
                actionPipeline,
                triggeredEffects,
                animOrchestrator,
                eventBus,
                sideService,
                fallbackActionResolver);
            turnService = new BattleTurnService(eventBus);
            turnService.OnTurnReady += HandleTurnReady;
            battleEndService = new BattleEndService(eventBus);
            battleEndService.OnBattleEnded += HandleBattleEnded;
            combatantDefeatedSubscription = eventBus.Subscribe<CombatantDefeatedEvent>(HandleCombatantDefeated);
        }

        private void PrepareContext()
        {
            RefreshCombatContext();
        }

        private void InitializeCombatants()
        {
            RebuildRoster(preservePlayerVitals: false, preserveEnemyVitals: false);
        }

        private void RebuildRoster(bool preservePlayerVitals, bool preserveEnemyVitals)
        {
            rosterCoordinator ??= new CombatantRosterCoordinator(new CombatantRosterService(), contextService);

            var currentPlayer = Player;
            var currentPlayerRuntime = referenceService?.PlayerRuntime ?? playerRuntime ?? currentPlayer?.CharacterRuntime;
            var currentEnemy = Enemy;
            var currentEnemyRuntime = referenceService?.EnemyRuntime ?? enemyRuntime ?? currentEnemy?.CharacterRuntime;

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
                Player = currentPlayer,
                PlayerRuntime = currentPlayerRuntime,
                Enemy = currentEnemy,
                EnemyRuntime = currentEnemyRuntime,
                HudManager = hudManager
            };

            var result = rosterCoordinator.RebuildRoster(
                rosterConfig,
                preservePlayerVitals,
                preserveEnemyVitals,
                context);

            ApplyRosterResult(result);

            turnService?.UpdateRoster(rosterSnapshot);
            OnCombatantsBound?.Invoke(AlliesList, EnemiesList);
        }

        private void RefreshCombatContext()
        {
            if (rosterCoordinator != null)
            {
                var update = rosterCoordinator.RefreshContext(context);
                if (update.Context != null || update.Enemy != null || update.EnemyRuntime != null || update.PlayerRuntime != null)
                {
                    ApplyContextUpdate(update);
                    return;
                }
            }
            else if (contextService != null)
            {
                var update = contextService.RefreshPlayerContext(
                    context,
                    Player,
                    referenceService?.PlayerRuntime ?? playerRuntime,
                    Enemy,
                    referenceService?.EnemyRuntime ?? enemyRuntime,
                    EnemiesList);

                ApplyContextUpdate(update);
                return;
            }

            var services = context != null ? context.Services : (config != null ? config.services : new BattleServices());
            var currentPlayer = Player;
            var currentEnemy = Enemy;
            var currentPlayerRuntime = referenceService?.PlayerRuntime ?? playerRuntime ?? currentPlayer?.CharacterRuntime;
            var currentEnemyRuntime = referenceService?.EnemyRuntime ?? enemyRuntime ?? currentEnemy?.CharacterRuntime;

            context = new CombatContext(
                currentPlayer,
                currentEnemy,
                currentPlayerRuntime,
                currentEnemyRuntime,
                services,
                actionCatalog);
        }

        private CharacterRuntime ResolveRuntime(CombatantState combatant, CharacterRuntime overrideRuntime)
        {
            if (contextService != null)
            {
                return contextService.ResolveRuntime(combatant, overrideRuntime);
            }

            if (combatant == null)
            {
                return null;
            }

            return combatant.CharacterRuntime != null
                ? combatant.CharacterRuntime
                : combatant.GetComponent<CharacterRuntime>();
        }

        private void ApplyContextUpdate(CombatContextUpdate update)
        {
            if (update.Context != null)
            {
                context = update.Context;
            }

            if (update.Enemy != null)
            {
                UpdateEnemyReference(update.Enemy, update.EnemyRuntime ?? enemyRuntime);
            }
            else if (update.EnemyRuntime != null)
            {
                UpdateEnemyReference(enemy, update.EnemyRuntime);
            }

            if (update.PlayerRuntime != null)
            {
                UpdatePlayerReference(player, update.PlayerRuntime);
            }
        }

        private void ApplyRosterResult(RosterCoordinatorResult result)
        {
            rosterSnapshot = result.Snapshot;

            UpdatePlayerReference(rosterSnapshot.Player, rosterSnapshot.PlayerRuntime);
            UpdateEnemyReference(rosterSnapshot.Enemy, rosterSnapshot.EnemyRuntime);

            ApplyContextUpdate(result.ContextUpdate);

            if (context == null)
            {
                var services = config != null ? config.services : new BattleServices();
                var currentPlayer = Player;
                var currentEnemy = Enemy;
                var currentPlayerRuntime = referenceService?.PlayerRuntime ?? playerRuntime ?? currentPlayer?.CharacterRuntime;
                var currentEnemyRuntime = referenceService?.EnemyRuntime ?? enemyRuntime ?? currentEnemy?.CharacterRuntime;

                context = new CombatContext(
                    currentPlayer,
                    currentEnemy,
                    currentPlayerRuntime,
                    currentEnemyRuntime,
                    services,
                    actionCatalog);
            }
        }

        private void UpdatePlayerReference(CombatantState value, CharacterRuntime runtime)
        {
            player = value;
            playerRuntime = runtime;
            referenceService?.SetPlayer(value, runtime);
        }

        private void UpdateEnemyReference(CombatantState value, CharacterRuntime runtime)
        {
            enemy = value;
            enemyRuntime = runtime;
            referenceService?.SetEnemy(value, runtime);
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

        public void SetRuntimeInputProvider(IBattleInputProvider provider)
        {
            string name = provider != null ? provider.GetType().Name : "(null)";
            LogDebug($"[BattleManagerV2] Runtime provider set to {name}.\nCall stack:\n{Environment.StackTrace}", this);
            inputProvider = provider;
            TryFlushPendingPlayerRequest();
        }

        public void SetTargetSelectionInteractor(ITargetSelectionInteractor interactor)
        {
            targetingCoordinator?.SetInteractor(interactor);
        }

        public void StartBattle()
        {
            state?.ResetToIdle();
            state?.Set(BattleState.AwaitingAction);
            turnService?.Begin();
        }

        public void ResetBattle()
        {
            ResetBattleCts();
            ComboPointScaling.Configure(config != null ? config.comboPointScaling : null);
            triggeredEffects?.Clear();
            ClearPendingPlayerRequest();
            RebuildRoster(preservePlayerVitals: false, preserveEnemyVitals: false);
            PrepareContext();
            TrySwitchToAnimationInstaller();
            state?.ResetToIdle();
            state?.Set(BattleState.AwaitingAction);
            turnService?.Stop();
            turnService?.Begin();
        }

        private void LogDebug(string message, UnityEngine.Object context = null)
        {
            if (!enableDebugLogs)
            {
                return;
            }

            if (context != null)
            {
                Debug.Log(message, context);
            }
            else
            {
                Debug.Log(message);
            }
        }

        private void ConfigureAnimationOrchestrator(BattleTimingProfile timingProfile)
        {
            if (useAnimationSystemInstaller)
            {
                animationSystemInstaller ??= AnimationSystemInstaller.Current;
            }

            if (useAnimationSystemInstaller &&
                animationSystemInstaller != null &&
                animationSystemInstaller.Orchestrator != null)
            {
                animOrchestrator = new BattleAnimationSystemBridge(animationSystemInstaller.Orchestrator);
            }
            else
            {
                animOrchestrator = new BattleAnimOrchestrator(eventBus, timingProfile);
            }

            RebuildPlayerActionExecutor();
        }

        private void TrySwitchToAnimationInstaller()
        {
            animationSystemInstaller ??= AnimationSystemInstaller.Current;
            if (animationSystemInstaller != null) useAnimationSystemInstaller = true;

            if (!useAnimationSystemInstaller)
            {
                return;
            }

            if (animationSystemInstaller?.Orchestrator == null)
            {
                return;
            }

            if (animOrchestrator is BattleAnimationSystemBridge bridge &&
                ReferenceEquals(bridge.InnerOrchestrator, animationSystemInstaller.Orchestrator))
            {
                return;
            }

            animOrchestrator = new BattleAnimationSystemBridge(animationSystemInstaller.Orchestrator);
            
            // Re-bind Overlay to the correct bus
            if (timedHitOverlay != null && animationSystemInstaller.EventBus != null)
            {
                Debug.Log("[BattleManagerV2] Re-binding TimedHitOverlay to AnimationSystemInstaller EventBus");
                timedHitOverlay.Initialize(animationSystemInstaller.EventBus);
            }

            enemyTurnCoordinator = new EnemyTurnCoordinator(
                actionCatalog,
                actionValidator,
                targetingCoordinator,
                actionPipeline,
                triggeredEffects,
                animOrchestrator,
                eventBus,
                sideService,
                fallbackActionResolver);

            RebuildPlayerActionExecutor();
        }

        private void RebuildPlayerActionExecutor()
        {
            playerActionExecutor = new PlayerActionExecutor(actionPipeline, timedResultResolver, triggeredEffects, eventBus);
        }

        private SelectionDraft currentDraft;

        private void HandlePlayerSelection(BattleSelection selection)
        {
            // Start the draft process
            currentDraft = SelectionDraft.Create(Player, selection.Action, 0, "Root"); // TODO: OriginMenu
            _ = ResolveDraftAsync(currentDraft, selection);
        }

        private async Task ResolveDraftAsync(SelectionDraft draft, BattleSelection initialSelection)
        {
            if (!draft.IsValid)
            {
                Debug.LogError("[BattleManagerV2] Invalid draft started.");
                return;
            }

            // 1. Resolve Targets (Build Phase)
            var playerResolution = targetResolutionService != null
                ? await targetResolutionService.ResolveForPlayerAsync(
                    draft.Actor,
                    initialSelection,
                    context,
                    AlliesList,
                    EnemiesList,
                    combatant => ResolveRuntime(combatant, combatant?.CharacterRuntime))
                : PlayerTargetResolution.Empty;

            if (this == null || !isActiveAndEnabled)
            {
                // Component destroyed or disabled during async resolution
                return;
            }

            // 2. Interpret Result (Strategy Phase)
            switch (playerResolution.Status)
            {
                case TargetResolutionStatus.Back:
                    BattleDiagnostics.Log("Orchestrator", "Draft Resolution: BACK. returning to menu.", draft.Actor);
                    // Do NOT commit. Do NOT end turn.
                    // The UI Interactor should have already handled the state transition to Menu.
                    currentDraft = currentDraft.ClearTargets();
                    // Nota: no re-llamamos RequestPlayerAction aquí para evitar reabrir Root.
                    // El stack UI ya quedó en el submenú previo (Atk/Mag/Item); el driver sigue en MenuState.
                    return;

                case TargetResolutionStatus.Cancelled:
                    BattleDiagnostics.Log("Orchestrator", "Draft Resolution: CANCELLED.", draft.Actor);
                    currentDraft = SelectionDraft.Empty;
                    return;

                case TargetResolutionStatus.Confirmed:
                    // Proceed to Commit
                    var targets = playerResolution.Result.TargetSet;
                    currentDraft = currentDraft.WithTargets(targets);
                    
                    await CommitSelectionDraft(currentDraft, initialSelection, playerResolution);
                    return;
                
                case TargetResolutionStatus.None:
                default:
                    Debug.LogWarning($"[BattleManagerV2] Unexpected resolution status: {playerResolution.Status}. Treating as Cancelled.");
                    currentDraft = SelectionDraft.Empty;
                    return;
            }
        }

        private async Task CommitSelectionDraft(SelectionDraft draft, BattleSelection selection, PlayerTargetResolution resolution)
        {
            if (this == null || !isActiveAndEnabled)
            {
                return;
            }

            if (draft.IsCommitted)
            {
                Debug.LogWarning("[BattleManagerV2] Attempted to commit an already committed draft.");
                return;
            }

            BattleDiagnostics.Log("Orchestrator", $"Committing Selection: {draft.Action?.id ?? "null"}");
            
            var currentPlayer = draft.Actor;
            int selectionId = ++cpSelectionCounter;

            // 1) Validate before consuming or ending turn
            if (!actionValidator.TryValidate(selection.Action, currentPlayer, context, selection.CpCharge, out var implementation))
            {
                BattleDiagnostics.Log("Orchestrator", "Validation Failed", currentPlayer);
                ExecuteFallback();
                return;
            }

            // 2) Consume Resources (Commit Phase)
            bool consumesCp = draft.Action != null &&
                              (draft.Action.costCP > 0 || selection.ChargeProfile != null || selection.TimedHitProfile != null);
            
            int extraCp = consumesCp ? cpIntent.ConsumeOnce(selectionId, "ActionCommit") : 0;
            
            // Mark as committed
            currentDraft = draft.MarkCommitted();

            // End Turn Signal (only confirmed commits reach EndTurn; Cancel/Back never do).
            cpIntent.EndTurn("CommittedOutcome");
            
            if (extraCp > 0)
            {
                selection = selection.WithCpCharge(selection.CpCharge + extraCp);
            }

            // 5. Update Context
            if (resolution.PrimaryEnemy != null && !resolution.Result.TargetSet.IsGroup)
            {
                var resolvedRuntime = resolution.PrimaryEnemyRuntime ?? ResolveRuntime(resolution.PrimaryEnemy, resolution.PrimaryEnemy?.CharacterRuntime);
                UpdateEnemyReference(resolution.PrimaryEnemy, resolvedRuntime);
            }

            RefreshCombatContext();

            // 6. Enrich Selection
            var enrichedSelection = selection.WithTargets(resolution.Result.TargetSet);
            if (resolution.PrimaryEnemy != null)
            {
                var targetTransform = resolution.PrimaryEnemy.transform;
                if (targetTransform != null)
                {
                    enrichedSelection = enrichedSelection.WithTargetTransform(targetTransform);
                }
            }
            if (enrichedSelection.TimedHitProfile != null)
            {
                enrichedSelection = enrichedSelection.WithTimedHitHandle(new TimedHitExecutionHandle(enrichedSelection.TimedHitResult));
            }

            // 7. Execute
            int cpBefore = currentPlayer != null ? currentPlayer.CurrentCP : 0;
            LastExecutedAction = enrichedSelection.Action;
            OnPlayerActionSelected?.Invoke(enrichedSelection, cpBefore);

            if (inputDriver != null)
            {
                inputDriver.SetMode(BattleInputMode.Execution);
                inputDriver.SetActiveActor(currentPlayer);
            }

            var targetsList = resolution.Result.Targets ?? Array.Empty<CombatantState>();
            var snapshot = new BattleV2.Orchestration.ExecutionSnapshot(AlliesList, EnemiesList, targetsList);

            var playbackTask = animOrchestrator != null
                ? animOrchestrator.PlayAsync(new ActionPlaybackRequest(currentPlayer, enrichedSelection, targetsList, CalculateAverageSpeed(), enrichedSelection.AnimationRecipeId))
                : Task.CompletedTask;
            var judgmentSeed = System.HashCode.Combine(selectionId, currentPlayer != null ? currentPlayer.GetInstanceID() : 0, enrichedSelection.Action != null ? enrichedSelection.Action.id.GetHashCode() : 0);
            var actionJudgment = ActionJudgment.FromSelection(enrichedSelection, currentPlayer, enrichedSelection.CpCharge, judgmentSeed);
            
            if (playerActionExecutor == null)
            {
                Debug.LogError("[BattleManagerV2] PlayerActionExecutor not initialised. Falling back.", this);
                ExecuteFallback();
                return;
            }

            await playerActionExecutor.ExecuteAsync(new PlayerActionExecutionContext
            {
                Manager = this,
                Player = currentPlayer,
                Selection = enrichedSelection,
                Implementation = implementation,
                CombatContext = context,
                Snapshot = snapshot,
                PlaybackTask = playbackTask,
                ComboPointsBefore = cpBefore,
                Judgment = actionJudgment,
                TryResolveBattleEnd = () => battleEndService != null && battleEndService.TryResolve(rosterSnapshot, currentPlayer, state),
                RefreshCombatContext = RefreshCombatContext,
                OnActionResolved = (resolved, before, after) => OnPlayerActionResolved?.Invoke(resolved, before, after),
                OnFallback = ExecuteFallback,
                SetState = state != null ? new Action<BattleState>(s => state.Set(s)) : null
            });

            await BattlePacingUtility.DelayGlobalAsync("PlayerTurn", currentPlayer, battleCts != null ? battleCts.Token : CancellationToken.None);
        }

        private void ExecuteFallback()
        {
            if (fallbackActionResolver == null || Player == null)
            {
                return;
            }

            if (fallbackActionResolver.TryResolve(Player, context, out var selection))
            {
                HandlePlayerSelection(selection);
            }
        }

        private void HandleBattleEnded(BattleResult result)
        {
            triggeredEffects?.Clear();
            DestroySpawnedPrefabs();
            turnService?.Stop();
        }

        private void DestroySpawnedPrefabs()
        {
            void DestroyList(IReadOnlyList<GameObject> list)
            {
                if (list == null)
                {
                    return;
                }

                for (int i = 0; i < list.Count; i++)
                {
                    var instance = list[i];
                    if (instance == null)
                    {
                        continue;
                    }

                    if (Application.isPlaying)
                    {
                        Destroy(instance);
                    }
                    else
                    {
                        DestroyImmediate(instance);
                    }
                }
            }

            DestroyList(rosterSnapshot.SpawnedPlayerInstances);
            DestroyList(rosterSnapshot.SpawnedEnemyInstances);
        }

        private async void HandleTurnReady(CombatantState actor)
        {
            try
            {
                timedHitInputRelay?.SetActor(actor);

                if (actor == null)
                {
                    battleEndService?.TryResolve(rosterSnapshot, Player, state);
                    return;
                }

                if (battleEndService != null && battleEndService.TryResolve(rosterSnapshot, Player, state))
                {
                    return;
                }

                if (IsAlly(actor))
                {
                    TriggerTurnPhase(actor);
                    state?.Set(BattleState.AwaitingAction);
                    
                    if (inputDriver != null)
                    {
                        inputDriver.SetMode(BattleInputMode.Menu);
                        inputDriver.SetActiveActor(actor);
                    }

                    RequestPlayerAction();
                }
                else
                {
                    cpIntent.EndTurn("EnemyTurn");
                    if (enemyTurnCoordinator != null)
                    {
                        var enemyContext = BuildEnemyTurnContext(actor);
                        await enemyTurnCoordinator.ExecuteAsync(enemyContext);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Battle cancelled/reset; safely ignore.
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BattleManagerV2] HandleTurnReady exception: {ex}");
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
            var currentPlayer = Player;
            if (currentPlayer == null || currentPlayer.IsDead())
            {
                state?.Set(BattleState.Defeat);
                return;
            }

            var available = actionCatalog?.BuildAvailableFor(currentPlayer, context);
            var currentEnemy = Enemy;
            if (available == null || available.Count == 0)
            {
                ExecuteFallback();
                return;
            }

            LogDebug($"[BattleManagerV2] RequestPlayerAction: provider={inputProvider?.GetType().Name ?? "(null)"} actions={available.Count}", this);

            var actionContext = new BattleActionContext
            {
                Player = currentPlayer,
                PlayerRuntime = referenceService?.PlayerRuntime ?? playerRuntime ?? currentPlayer?.CharacterRuntime,
                Enemy = currentEnemy,
                EnemyRuntime = referenceService?.EnemyRuntime ?? enemyRuntime ?? currentEnemy?.CharacterRuntime,
                AvailableActions = available,
                Context = context,
                MaxCpCharge = currentPlayer != null ? currentPlayer.CurrentCP : 0,
                PlayerStats = currentPlayer != null ? currentPlayer.FinalStats : default,
                EnemyStats = currentEnemy != null ? currentEnemy.FinalStats : default
            };

            if (inputProvider == null)
            {
                cpIntent.BeginTurn(currentPlayer.CurrentCP);
                QueuePendingPlayerRequest(actionContext, HandlePlayerSelection, ExecuteFallback);
                return;
            }

            cpIntent.BeginTurn(currentPlayer.CurrentCP);
            DispatchToInputProvider(actionContext, HandlePlayerSelection, ExecuteFallback);
        }

        private void TriggerTurnPhase(CombatantState actor)
        {
            if (actor == null)
            {
                return;
            }

            var orchestrator = animationSystemInstaller?.Orchestrator;
            if (orchestrator == null)
            {
                return;
            }

            string sessionId = !string.IsNullOrWhiteSpace(actor.name)
                ? actor.name
                : $"turn_{actor.GetInstanceID()}";

            var participants = IsAlly(actor) ? AlliesList : EnemiesList;
            var context = new AnimationContext(sessionId, actor, participants);
            orchestrator.EnterPhase(BattlePhase.Turn, context);
        }

        private EnemyTurnContext BuildEnemyTurnContext(CombatantState attacker)
        {
            var target = Player;
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
                () => battleEndService != null && battleEndService.TryResolve(rosterSnapshot, Player, state),
                RefreshCombatContext,
                battleCts != null ? battleCts.Token : CancellationToken.None);
        }

        private CombatContext CreateEnemyCombatContext(CombatantState attacker, CombatantState target)
        {
            var currentPlayerRuntime = referenceService?.PlayerRuntime ?? playerRuntime ?? Player?.CharacterRuntime;

            if (rosterCoordinator != null)
            {
                return rosterCoordinator.CreateEnemyContext(attacker, target, context, currentPlayerRuntime, actionCatalog, config);
            }

            if (contextService != null)
            {
                return contextService.CreateEnemyContext(attacker, target, context, currentPlayerRuntime);
            }

            var services = context != null ? context.Services : (config != null ? config.services : new BattleServices());
            return new CombatContext(
                attacker,
                target,
                ResolveRuntime(attacker, attacker?.CharacterRuntime),
                ResolveRuntime(target, target?.CharacterRuntime ?? currentPlayerRuntime),
                services,
                actionCatalog);
        }

        private void QueuePendingPlayerRequest(
            BattleActionContext context,
            Action<BattleSelection> onSelected,
            Action onCancel)
        {
            pendingPlayerRequest ??= new PendingPlayerRequest();
            pendingPlayerRequest.Context = context;
            pendingPlayerRequest.OnSelected = onSelected;
            pendingPlayerRequest.OnCancel = onCancel;

            Debug.LogWarning("[BattleManagerV2] Player action request queued; waiting for a runtime input provider.", this);
        }

        private void DispatchToInputProvider(
            BattleActionContext context,
            Action<BattleSelection> onSelected,
            Action onCancel)
        {
            if (inputProvider == null)
            {
                return;
            }

            if (pendingPlayerRequest != null)
            {
                pendingPlayerRequest.Clear();
                pendingPlayerRequest = null;
            }

            void CancelWrapper()
            {
                // Limpia la selección CP sin terminar el turno.
                cpIntent.ResetSelection("SelectionCanceled");
                onCancel?.Invoke();
            }

            inputProvider.RequestAction(context, onSelected, CancelWrapper);
        }

        private void TryFlushPendingPlayerRequest()
        {
            if (inputProvider == null || pendingPlayerRequest == null)
            {
                return;
            }

            var queued = pendingPlayerRequest;
            pendingPlayerRequest = null;
            inputProvider.RequestAction(queued.Context, queued.OnSelected, queued.OnCancel);
            queued.Clear();
        }

        private void ClearPendingPlayerRequest()
        {
            if (pendingPlayerRequest == null)
            {
                return;
            }

            pendingPlayerRequest.Clear();
            pendingPlayerRequest = null;
        }

        private void HandleCombatantDefeated(CombatantDefeatedEvent evt)
        {
            if (evt.Combatant == null || rosterCoordinator == null)
            {
                return;
            }

            referenceService?.Clear(evt.Combatant);
            var result = rosterCoordinator.RefreshAfterDeath(evt.Combatant, context);
            ApplyRosterResult(result);

            turnService?.UpdateRoster(rosterSnapshot);
            OnCombatantsBound?.Invoke(AlliesList, EnemiesList);

            battleEndService?.TryResolve(rosterSnapshot, Player, state);
        }

        private TimedHitInputRelay FindTimedHitInputRelay()
        {
#if UNITY_2023_1_OR_NEWER
            return FindFirstObjectByType<TimedHitInputRelay>();
#else
            return FindObjectOfType<TimedHitInputRelay>();
#endif
        }

        private void ResetBattleCts()
        {
            CancelBattleCts();
            battleCts = new CancellationTokenSource();
        }

        private void CancelBattleCts()
        {
            if (battleCts == null)
            {
                return;
            }

            try
            {
                battleCts.Cancel();
            }
            catch
            {
                // ignore
            }

            battleCts.Dispose();
            battleCts = null;
        }
    }

    internal sealed class PendingPlayerRequest
    {
        public BattleActionContext Context;
        public Action<BattleSelection> OnSelected;
        public Action OnCancel;

        public void Clear()
        {
            Context = default;
            OnSelected = null;
            OnCancel = null;
        }
    }
}
