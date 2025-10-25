using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using BattleV2.Actions;
using BattleV2.Anim;
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
        private ScriptableObject enemyDropTable;
        private ITimedHitRunner timedHitRunner;

        private ITurnController turnController;
        private ITargetingCoordinator targetingCoordinator;
        private IActionPipeline actionPipeline;
        private ITriggeredEffectsService triggeredEffects;
        private IBattleAnimOrchestrator animOrchestrator;
        private IBattleEventBus eventBus;

        private readonly List<CombatantState> allies = new();
        private readonly List<CombatantState> enemies = new();
        private readonly List<GameObject> spawnedPlayerInstances = new();
        private readonly List<GameObject> spawnedEnemyInstances = new();

        public event Action<BattleSelection, int> OnPlayerActionSelected;
        public event Action<BattleSelection, int, int> OnPlayerActionResolved;
        public event Action<IReadOnlyList<CombatantState>, IReadOnlyList<CombatantState>> OnCombatantsBound;

        public BattleActionData LastExecutedAction { get; private set; }
        public ITimedHitRunner TimedHitRunner => timedHitRunner ?? InstantTimedHitRunner.Shared;
        public TargetResolverRegistry TargetResolvers { get; private set; }
        public CombatantState Player => player;
        public CombatantState Enemy => enemy;
        public IReadOnlyList<CombatantState> Allies => allies;
        public IReadOnlyList<CombatantState> Enemies => enemies;
        public float PreActionDelaySeconds
        {
            get => preActionDelaySeconds;
            set => preActionDelaySeconds = Mathf.Max(0f, value);
        }
        public ScriptableObject EnemyDropTable => enemyDropTable;
        public IReadOnlyList<GameObject> SpawnedPlayerInstances => spawnedPlayerInstances;
        public IReadOnlyList<GameObject> SpawnedEnemyInstances => spawnedEnemyInstances;
        public GameObject SpawnedEnemyInstance => spawnedEnemyInstances.Count > 0 ? spawnedEnemyInstances[0] : null;
        public GameObject SpawnedPlayerInstance => spawnedPlayerInstances.Count > 0 ? spawnedPlayerInstances[0] : null;

        private void Awake()
        {
            BootstrapServices();
            PrepareContext();
            InitializeCombatants();
        }

        private void OnEnable()
        {
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
            allies.Clear();
            enemies.Clear();
            hudManager?.Clear();
            DestroyAutoSpawned(spawnedPlayerInstances);
            DestroyAutoSpawned(spawnedEnemyInstances);
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
            triggeredEffects = new TriggeredEffectsService(eventBus);
            animOrchestrator = new BattleAnimOrchestrator(eventBus);
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
            EnsurePlayerSpawned();
            EnsureEnemySpawned();
            BindCombatants(preservePlayerVitals: false, preserveEnemyVitals: false);
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
            PrepareContext();
            InitializeCombatants();
            state?.ResetToIdle();
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
                allies,
                enemies);

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

                triggeredEffects.Enqueue(selection.Action, selection.Targets);
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
                turnController?.Rebuild(allies, enemies);
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

        private void EnsureEnemySpawned()
        {
            enemies.Clear();

            if (!autoSpawnEnemy)
            {
                enemyDropTable = null;
                if (enemy != null)
                {
                    enemies.Add(enemy);
                    enemyRuntime = ResolveRuntime(enemy, enemyRuntime);
                }
                return;
            }

            DestroyAutoSpawned(spawnedEnemyInstances);
            enemyDropTable = null;

            IReadOnlyList<CombatantLoadoutEntry> entries = enemyEncounterLoadout != null && enemyEncounterLoadout.Enemies.Count > 0
                ? enemyEncounterLoadout.Enemies
                : null;

            Vector3[] patternOffsets = null;
            if (enemyEncounterLoadout?.SpawnPattern != null && entries != null)
            {
                enemyEncounterLoadout.SpawnPattern.TryGetOffsets(entries.Count, out patternOffsets);
            }

            var fallbackParent = IsSceneTransform(enemySpawnPoint) ? enemySpawnPoint : transform;

            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    if (!entry.IsValid)
                    {
                        continue;
                    }

                    Transform spawnTransform = ResolveSpawnTransform(enemySpawnPoints, enemySpawnPoint, i);
                    var parent = spawnTransform != null ? spawnTransform : fallbackParent;
                    Vector3 offset = entry.SpawnOffset;
                    if (spawnTransform == null && patternOffsets != null && patternOffsets.Length > 0)
                    {
                        offset += patternOffsets[Mathf.Clamp(i, 0, patternOffsets.Length - 1)];
                    }

                    Vector3 worldPosition = parent.position + offset;
                    Quaternion worldRotation = parent.rotation;

                    var combatant = SpawnCombatant(entry, parent, worldPosition, worldRotation, spawnedEnemyInstances, out var dropTable);
                    if (combatant == null)
                    {
                        continue;
                    }

                    enemies.Add(combatant);
                    if (enemyDropTable == null && dropTable != null)
                    {
                        enemyDropTable = dropTable;
                    }
                }
            }
            else if (enemyLoadout != null && enemyLoadout.IsValid)
            {
                var entry = new CombatantLoadoutEntry(enemyLoadout.EnemyPrefab, enemyLoadout.SpawnOffset, enemyLoadout.DropTable);
                var parent = ResolveSpawnTransform(enemySpawnPoints, enemySpawnPoint, 0) ?? fallbackParent;
                Vector3 worldPosition = parent.position + entry.SpawnOffset;
                Quaternion worldRotation = parent.rotation;

                var combatant = SpawnCombatant(entry, parent, worldPosition, worldRotation, spawnedEnemyInstances, out var dropTable);
                if (combatant != null)
                {
                    enemies.Add(combatant);
                    if (dropTable != null)
                    {
                        enemyDropTable = dropTable;
                    }
                }
            }

            if (enemies.Count > 0)
            {
                enemy = enemies[0];
                enemyRuntime = ResolveRuntime(enemy, enemyRuntime);
            }
            else
            {
                enemy = null;
                enemyRuntime = null;
            }
        }

        private void EnsurePlayerSpawned()
        {
            allies.Clear();

            if (!autoSpawnPlayer)
            {
                if (player != null)
                {
                    allies.Add(player);
                    playerRuntime = ResolveRuntime(player, playerRuntime);
                }
                return;
            }

            DestroyAutoSpawned(spawnedPlayerInstances);

            var fallbackParent = IsSceneTransform(playerSpawnPoint) ? playerSpawnPoint : transform;

            if (playerPartyLoadout != null && playerPartyLoadout.Members.Count > 0)
            {
                var members = playerPartyLoadout.Members;
                for (int i = 0; i < members.Count; i++)
                {
                    var entry = members[i];
                    if (!entry.IsValid)
                    {
                        continue;
                    }

                    Transform spawnTransform = ResolveSpawnTransform(playerSpawnPoints, playerSpawnPoint, i);
                    var parent = spawnTransform != null ? spawnTransform : fallbackParent;
                    Vector3 worldPosition = parent.position + entry.SpawnOffset;
                    Quaternion worldRotation = parent.rotation;

                    var combatant = SpawnCombatant(entry, parent, worldPosition, worldRotation, spawnedPlayerInstances, out _);
                    if (combatant == null)
                    {
                        continue;
                    }

                    allies.Add(combatant);
                }
            }
            else if (playerLoadout != null && playerLoadout.IsValid)
            {
                var entry = new CombatantLoadoutEntry(playerLoadout.PlayerPrefab, playerLoadout.SpawnOffset);
                var parent = ResolveSpawnTransform(playerSpawnPoints, playerSpawnPoint, 0) ?? fallbackParent;
                Vector3 worldPosition = parent.position + entry.SpawnOffset;
                Quaternion worldRotation = parent.rotation;

                var combatant = SpawnCombatant(entry, parent, worldPosition, worldRotation, spawnedPlayerInstances, out _);
                if (combatant != null)
                {
                    allies.Add(combatant);
                }
            }

            if (allies.Count > 0)
            {
                player = allies[0];
                playerRuntime = ResolveRuntime(player, playerRuntime);
            }
            else
            {
                player = null;
                playerRuntime = null;
            }
        }

        private void BindCombatants(bool preservePlayerVitals, bool preserveEnemyVitals)
        {
            hudManager?.Clear();

            var boundAllies = new List<CombatantState>(allies.Count);
            var boundEnemies = new List<CombatantState>(enemies.Count);

            for (int i = 0; i < allies.Count; i++)
            {
                var combatant = allies[i];
                if (CombatantBinder.TryBind(combatant, preservePlayerVitals, out var result))
                {
                    boundAllies.Add(result.Combatant);
                    if (i == 0)
                    {
                        player = result.Combatant;
                        playerRuntime = result.Runtime;
                    }
                }
            }

            if (boundAllies.Count == 0 && player != null && CombatantBinder.TryBind(player, preservePlayerVitals, out var playerResult))
            {
                boundAllies.Add(playerResult.Combatant);
                player = playerResult.Combatant;
                playerRuntime = playerResult.Runtime;
            }
            else if (boundAllies.Count == 0)
            {
                playerRuntime = ResolveRuntime(player, playerRuntime);
            }

            allies.Clear();
            allies.AddRange(boundAllies);

            for (int i = 0; i < enemies.Count; i++)
            {
                var combatant = enemies[i];
                if (CombatantBinder.TryBind(combatant, preserveEnemyVitals, out var result))
                {
                    boundEnemies.Add(result.Combatant);
                    if (i == 0)
                    {
                        enemy = result.Combatant;
                        enemyRuntime = result.Runtime;
                    }
                }
            }

            if (boundEnemies.Count == 0 && enemy != null && CombatantBinder.TryBind(enemy, preserveEnemyVitals, out var enemyResult))
            {
                boundEnemies.Add(enemyResult.Combatant);
                enemy = enemyResult.Combatant;
                enemyRuntime = enemyResult.Runtime;
            }
            else if (boundEnemies.Count == 0)
            {
                enemyRuntime = ResolveRuntime(enemy, enemyRuntime);
            }

            enemies.Clear();
            enemies.AddRange(boundEnemies);

            if (allies.Count == 0)
            {
                player = null;
                playerRuntime = null;
            }

            if (enemies.Count == 0)
            {
                enemy = null;
                enemyRuntime = null;
            }

            hudManager?.RegisterCombatants(allies, isEnemy: false);
            hudManager?.RegisterCombatants(enemies, isEnemy: true);

            OnCombatantsBound?.Invoke(allies, enemies);
            turnController.Rebuild(allies, enemies);
            RefreshCombatContext();
        }

        private void DestroyAutoSpawned(List<GameObject> instances)
        {
            if (instances == null)
            {
                return;
            }

            for (int i = 0; i < instances.Count; i++)
            {
                var instance = instances[i];
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

            instances.Clear();
        }

        private Transform ResolveSpawnTransform(Transform[] points, Transform fallback, int index)
        {
            if (points != null && points.Length > 0)
            {
                int clamped = Mathf.Clamp(index, 0, points.Length - 1);
                Transform candidate = points[clamped];
                if (IsSceneTransform(candidate))
                {
                    return candidate;
                }
            }

            if (IsSceneTransform(fallback))
            {
                return fallback;
            }

            return transform;
        }

        private bool IsSceneTransform(Transform target)
        {
            return target != null && target.gameObject.scene.IsValid();
        }

        private CombatantState SpawnCombatant(
            CombatantLoadoutEntry entry,
            Transform parentTransform,
            Vector3 worldPosition,
            Quaternion worldRotation,
            List<GameObject> instanceCollector,
            out ScriptableObject dropTable)
        {
            dropTable = null;

            if (!entry.IsValid)
            {
                return null;
            }

            Transform parent = parentTransform != null ? parentTransform : transform;

            GameObject instance = Instantiate(entry.Prefab, worldPosition, worldRotation, parent);
            instanceCollector?.Add(instance);

            CombatantState combatant = instance.GetComponentInChildren<CombatantState>();
            if (combatant == null)
            {
                Debug.LogError($"[BattleManagerV2] Spawned prefab '{entry.Prefab.name}' missing CombatantState. Destroying instance.");
                instanceCollector?.Remove(instance);
                Destroy(instance);
                return null;
            }

            dropTable = entry.DropTable;
            return combatant;
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
                    allies,
                    enemies);

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
                    triggeredEffects.Enqueue(enrichedSelection.Action, enrichedSelection.Targets);
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
            turnController?.Rebuild(allies, enemies);
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

            bool enemyAlive = false;
            for (int i = 0; i < enemies.Count; i++)
            {
                var combatant = enemies[i];
                if (combatant != null && combatant.IsAlive)
                {
                    enemyAlive = true;
                    break;
                }
            }

            if (!enemyAlive)
            {
                state?.Set(BattleState.Victory);
                return true;
            }

            return false;
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

            if (allies.Contains(current))
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
