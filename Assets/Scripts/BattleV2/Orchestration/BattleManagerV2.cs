using System;
using System.Collections;
using System.Threading.Tasks;
using BattleV2.Actions;
using BattleV2.Anim;
using BattleV2.Charge;
using BattleV2.Core;
using BattleV2.Execution;
using BattleV2.Execution.TimedHits;
using BattleV2.Providers;
using HalloweenJam.Combat;
using UnityEngine;

namespace BattleV2.Orchestration
{
    /// <summary>
    /// New battle manager that delegates UI/AI decisions to providers.
    /// </summary>
    public class BattleManagerV2 : MonoBehaviour
    {
        [Header("Core")]
        [SerializeField] private BattleStateController state;
        [SerializeField] private BattleConfig config;
        [SerializeField] private ActionCatalog actionCatalog;
        [SerializeField] private ScriptableObject inputProviderAsset;
        [SerializeField] private MonoBehaviour inputProviderComponent;

        [Header("Entities")]
        [SerializeField] private CombatantState player;
        [SerializeField] private CharacterRuntime playerRuntime;
        [SerializeField] private CombatantState enemy;
        [SerializeField] private CharacterRuntime enemyRuntime;

        [Header("Player Spawn")]
        [SerializeField] private bool autoSpawnPlayer = true;
        [SerializeField] private PlayerLoadout playerLoadout;
        [SerializeField] private Transform playerSpawnPoint;

        [Header("Enemy Spawn")]
        [SerializeField] private bool autoSpawnEnemy = true;
        [SerializeField] private EnemyLoadout enemyLoadout;
        [SerializeField] private Transform enemySpawnPoint;

        [Header("Pipeline Tuning")]
        [SerializeField, Min(0f)] private float preActionDelaySeconds = 0.12f;

        private IBattleInputProvider inputProvider;
        private CombatContext context;
        private bool animationLocked;
        private Action pendingEnemyTurn;
        private BattleSelection pendingPlayerSelection;
        private IAction pendingPlayerAction;
        private int pendingPlayerCpBefore;
        private bool waitingForPlayerAnimation;
        private bool waitingForEnemyAnimation;
        private Coroutine playerActionDelayRoutine;
        private GameObject spawnedPlayerInstance;
        private GameObject spawnedEnemyInstance;
        private ScriptableObject enemyDropTable;
        private IActionPipelineFactory actionPipelineFactory;
        private ITimedHitRunner timedHitRunner;

        public BattleActionData LastExecutedAction { get; private set; }
        public ITimedHitRunner TimedHitRunner => timedHitRunner ?? InstantTimedHitRunner.Shared;
        public CombatantState Player => player;
        public CombatantState Enemy => enemy;
        public CharacterRuntime PlayerRuntime => playerRuntime;
        public CharacterRuntime EnemyRuntime => enemyRuntime;
        public float PreActionDelaySeconds
        {
            get => preActionDelaySeconds;
            set => preActionDelaySeconds = Mathf.Max(0f, value);
        }
        public GameObject SpawnedEnemyInstance => spawnedEnemyInstance;
        public GameObject SpawnedPlayerInstance => spawnedPlayerInstance;
        public ScriptableObject EnemyDropTable => enemyDropTable;
        public event Action<BattleSelection, int> OnPlayerActionSelected;
        public event Action<BattleSelection, int, int> OnPlayerActionResolved;
        public event Action<CombatantState, CombatantState> OnCombatantsBound;

        private void Awake()
        {
            if (config != null)
            {
                if (actionCatalog == null)
                {
                    actionCatalog = config.actionCatalog;
                }

                if (inputProviderAsset == null && inputProviderComponent == null && config.inputProvider != null)
                {
                    if (config.inputProvider is MonoBehaviour comp)
                    {
                        inputProviderComponent = comp;
                    }
                    else if (config.inputProvider is ScriptableObject asset)
                    {
                        inputProviderAsset = asset;
                    }
                }
            }

            inputProvider = ResolveProvider();

            if (inputProvider == null)
            {
                BattleLogger.Warn("BattleManager", "No input provider assigned; creating auto provider.");
                inputProvider = ScriptableObject.CreateInstance<AutoBattleInputProvider>();
            }

            actionPipelineFactory = new DefaultActionPipelineFactory(this);
            timedHitRunner = InstantTimedHitRunner.Shared;

            ComboPointScaling.Configure(config != null ? config.comboPointScaling : null);

            EnsurePlayerSpawned();
            EnsureEnemySpawned();
            BindCombatants(preservePlayerVitals: false, preserveEnemyVitals: false);

            var services = config != null ? config.services : new BattleServices();
            context = new CombatContext(
                player,
                enemy,
                playerRuntime,
                enemyRuntime,
                services,
                actionCatalog);
        }

        private void OnEnable()
        {
            BattleEvents.OnLockChanged += HandleAnimationLockChanged;
            BattleEvents.OnAnimationStageCompleted += HandleAnimationStageCompleted;
        }

        private void OnDisable()
        {
            BattleEvents.OnLockChanged -= HandleAnimationLockChanged;
            BattleEvents.OnAnimationStageCompleted -= HandleAnimationStageCompleted;
            pendingEnemyTurn = null;
            if (playerActionDelayRoutine != null)
            {
                StopCoroutine(playerActionDelayRoutine);
                playerActionDelayRoutine = null;
            }
            spawnedPlayerInstance = null;
            spawnedEnemyInstance = null;
            animationLocked = false;
            waitingForPlayerAnimation = false;
            waitingForEnemyAnimation = false;
            pendingPlayerAction = null;
            pendingPlayerSelection = default;
            pendingPlayerCpBefore = 0;
        }

        private IBattleInputProvider ResolveProvider()
        {
            if (inputProviderComponent != null)
            {
                if (inputProviderComponent is IBattleInputProvider componentProvider)
                {
                    return componentProvider;
                }
                BattleLogger.Warn("BattleManager", $"Input provider component '{inputProviderComponent.name}' does not implement IBattleInputProvider.");
            }

            if (inputProviderAsset != null)
            {
                if (inputProviderAsset is IBattleInputProvider assetProvider)
                {
                    return assetProvider;
                }
                BattleLogger.Warn("BattleManager", $"Input provider asset '{inputProviderAsset.name}' does not implement IBattleInputProvider.");
            }

            if (config != null && config.inputProvider != null)
            {
                if (config.inputProvider is MonoBehaviour configComponent)
                {
                    inputProviderComponent = configComponent;
                    return ResolveProvider();
                }
                if (config.inputProvider is ScriptableObject configAsset)
                {
                    inputProviderAsset = configAsset;
                    return ResolveProvider();
                }
                BattleLogger.Warn("BattleManager", $"Config input provider '{config.inputProvider.name}' is not a valid provider type.");
            }

            return null;
        }

        private void BindCombatants(bool preservePlayerVitals, bool preserveEnemyVitals)
        {
            CombatantBinder.BindingResult playerBinding = default;
            CombatantBinder.BindingResult enemyBinding = default;

            if (CombatantBinder.TryBind(player, preservePlayerVitals, out var playerResult))
            {
                playerBinding = playerResult;
                player = playerResult.Combatant;
                playerRuntime = playerResult.Runtime;
            }
            else
            {
                playerRuntime = ResolveRuntimeReference(player, playerRuntime);
            }

            if (CombatantBinder.TryBind(enemy, preserveEnemyVitals, out var enemyResult))
            {
                enemyBinding = enemyResult;
                enemy = enemyResult.Combatant;
                enemyRuntime = enemyResult.Runtime;
            }
            else
            {
                enemyRuntime = ResolveRuntimeReference(enemy, enemyRuntime);
            }

            var boundPlayer = playerBinding.Combatant != null ? playerBinding.Combatant : player;
            var boundEnemy = enemyBinding.Combatant != null ? enemyBinding.Combatant : enemy;
            OnCombatantsBound?.Invoke(boundPlayer, boundEnemy);
        }

        private static CharacterRuntime ResolveRuntimeReference(CombatantState combatant, CharacterRuntime overrideRuntime)
        {
            if (overrideRuntime != null)
            {
                return overrideRuntime;
            }

            if (combatant == null)
            {
                return null;
            }

            if (combatant.CharacterRuntime != null)
            {
                return combatant.CharacterRuntime;
            }

            return combatant.GetComponent<CharacterRuntime>();
        }

        public void StartBattle()
        {
            state.ResetToIdle();
            state.Set(BattleState.AwaitingAction);
            RequestPlayerAction();
        }

        public void ResetBattle()
        {
            ComboPointScaling.Configure(config != null ? config.comboPointScaling : null);

            EnsurePlayerSpawned();
            EnsureEnemySpawned();
            BindCombatants(preservePlayerVitals: false, preserveEnemyVitals: false);

            var services = context != null
                ? context.Services
                : (config != null ? config.services : new BattleServices());

            context = new CombatContext(
                player,
                enemy,
                playerRuntime,
                enemyRuntime,
                services,
                actionCatalog);

            state.ResetToIdle();
        }

        private void RequestPlayerAction()
        {
            if (state.State != BattleState.AwaitingAction)
            {
                BattleLogger.Warn("BattleManager", $"RequestPlayerAction called in state {state.State}");
                return;
            }

            var available = actionCatalog.BuildAvailableFor(player, context);
            if (available.Count == 0)
            {
                BattleLogger.Warn("BattleManager", "No available actions; executing fallback.");
                ExecuteAutoFallback();
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

            inputProvider.RequestAction(actionContext, ExecuteAction, ExecuteAutoFallback);
        }

        private void ExecuteAction(BattleSelection selection)
        {
            var selected = selection.Action;

            if (selected == null)
            {
                BattleLogger.Warn("BattleManager", "Selected action null; using fallback.");
                ExecuteAutoFallback();
                return;
            }

            var impl = actionCatalog.Resolve(selected);
            if (impl == null)
            {
                BattleLogger.Warn("BattleManager", $"Action {selected.id} has no implementation; using fallback.");
                ExecuteAutoFallback();
                return;
            }

            if (player == null)
            {
                BattleLogger.Error("BattleManager", "Player missing; cannot execute action.");
                return;
            }

            int totalCpRequired = impl.CostCP + Mathf.Max(0, selection.CpCharge);
            if (player.CurrentCP < totalCpRequired)
            {
                BattleLogger.Warn("BattleManager", $"Not enough CP for {selected.id} (needs {totalCpRequired}, has {player.CurrentCP}).");
                ExecuteAutoFallback();
                return;
            }

            if (!impl.CanExecute(player, context, selection.CpCharge))
            {
                BattleLogger.Warn("BattleManager", $"Action {selected.id} cannot execute; using fallback.");
                ExecuteAutoFallback();
                return;
            }

            LastExecutedAction = selected;
            BattleLogger.Log("Execute", $"Action {selected.id} starting.");
            state.Set(BattleState.Resolving);

            pendingPlayerSelection = selection;
            pendingPlayerAction = impl;
            pendingPlayerCpBefore = player.CurrentCP;
            waitingForPlayerAnimation = false;

            TryExecutePendingPlayerAction();
        }

        private void QueueEnemyTurn(Action execute)
        {
            if (execute == null)
            {
                return;
            }

            pendingEnemyTurn += execute;
            TryExecutePendingEnemyTurn();
        }

        private void TryExecutePendingPlayerAction()
        {
            if (pendingPlayerAction == null)
            {
                return;
            }

            if (waitingForPlayerAnimation || playerActionDelayRoutine != null)
            {
                return;
            }

            var selection = pendingPlayerSelection;
            var impl = pendingPlayerAction;
            int cpBefore = pendingPlayerCpBefore;

            pendingPlayerAction = null;
            pendingPlayerSelection = default;
            pendingPlayerCpBefore = 0;

            float delay = Mathf.Max(0f, preActionDelaySeconds);
            if (delay > 0f)
            {
                playerActionDelayRoutine = StartCoroutine(ExecutePlayerActionAfterDelay(selection, impl, cpBefore, delay));
                return;
            }

            DispatchPlayerAction(selection, impl, cpBefore);
        }

        private void DispatchPlayerAction(BattleSelection selection, IAction implementation, int cpBefore)
        {
            OnPlayerActionSelected?.Invoke(selection, cpBefore);
            RunPlayerActionPipeline(selection, implementation, cpBefore);
        }

        private IEnumerator ExecutePlayerActionAfterDelay(
            BattleSelection selection,
            IAction implementation,
            int cpBefore,
            float delaySeconds)
        {
            waitingForPlayerAnimation = true;
            yield return new WaitForSeconds(delaySeconds);
            waitingForPlayerAnimation = false;
            playerActionDelayRoutine = null;
            DispatchPlayerAction(selection, implementation, cpBefore);
        }

        private void EnsureEnemySpawned()
        {
            if (!autoSpawnEnemy || enemy != null)
            {
                enemyRuntime = ResolveRuntimeReference(enemy, enemyRuntime);
                return;
            }

            if (enemyLoadout == null || !enemyLoadout.IsValid)
            {
                BattleLogger.Warn("BattleManager", "Auto enemy spawn enabled but loadout is missing or invalid.");
                return;
            }

            var prefab = enemyLoadout.EnemyPrefab;
            if (prefab == null)
            {
                BattleLogger.Warn("BattleManager", "Enemy loadout is missing a prefab; cannot spawn enemy.");
                return;
            }

            Transform parent = enemySpawnPoint != null ? enemySpawnPoint : transform;
            Vector3 basePosition = enemySpawnPoint != null ? enemySpawnPoint.position : parent.position;
            Quaternion baseRotation = enemySpawnPoint != null ? enemySpawnPoint.rotation : parent.rotation;

            var instance = Instantiate(prefab, basePosition + enemyLoadout.SpawnOffset, baseRotation, parent);
            spawnedEnemyInstance = instance;

            var combatant = instance.GetComponentInChildren<CombatantState>();
            if (combatant == null)
            {
                BattleLogger.Error("BattleManager", $"Spawned enemy prefab '{prefab.name}' missing CombatantState component. Destroying instance.");
                Destroy(instance);
                spawnedEnemyInstance = null;
                return;
            }

            enemy = combatant;
            enemyRuntime = ResolveRuntimeReference(enemy, enemyRuntime);
            enemy.EnsureInitialized(enemyRuntime);

            enemyDropTable = enemyLoadout.DropTable;
            BattleLogger.Log("BattleManager", $"Spawned enemy '{enemy.DisplayName}' from loadout '{enemyLoadout.name}'.");
        }

        private void EnsurePlayerSpawned()
        {
            if (!autoSpawnPlayer || player != null)
            {
                playerRuntime = ResolveRuntimeReference(player, playerRuntime);
                return;
            }

            if (playerLoadout == null || !playerLoadout.IsValid)
            {
                BattleLogger.Warn("BattleManager", "Auto player spawn enabled but loadout is missing or invalid.");
                return;
            }

            var prefab = playerLoadout.PlayerPrefab;
            if (prefab == null)
            {
                BattleLogger.Warn("BattleManager", "Player loadout is missing a prefab; cannot spawn player.");
                return;
            }

            Transform parent = playerSpawnPoint != null ? playerSpawnPoint : transform;
            Vector3 basePosition = playerSpawnPoint != null ? playerSpawnPoint.position : parent.position;
            Quaternion baseRotation = playerSpawnPoint != null ? playerSpawnPoint.rotation : parent.rotation;

            var instance = Instantiate(prefab, basePosition + playerLoadout.SpawnOffset, baseRotation, parent);
            spawnedPlayerInstance = instance;

            var combatant = instance.GetComponentInChildren<CombatantState>();
            if (combatant == null)
            {
                BattleLogger.Error("BattleManager", $"Spawned player prefab '{prefab.name}' missing CombatantState component. Destroying instance.");
                Destroy(instance);
                spawnedPlayerInstance = null;
                return;
            }

            player = combatant;
            playerRuntime = ResolveRuntimeReference(player, playerRuntime);
            player.EnsureInitialized(playerRuntime);
        }

        private void TryExecutePendingEnemyTurn()
        {
            if (pendingEnemyTurn == null)
            {
                return;
            }

            if (waitingForEnemyAnimation || animationLocked)
            {
                return;
            }

            waitingForEnemyAnimation = false;
            var execute = pendingEnemyTurn;
            pendingEnemyTurn = null;
            execute?.Invoke();
        }

        private void ExecuteEnemyTurn(Action onComplete)
        {
            var enemyContext = new CombatContext(
                enemy,
                player,
                enemyRuntime ?? enemy?.CharacterRuntime,
                playerRuntime ?? player?.CharacterRuntime,
                context.Services,
                actionCatalog);
            var available = actionCatalog.BuildAvailableFor(enemy, enemyContext);
            if (available.Count == 0)
            {
                BattleLogger.Warn("Enemy", "Enemy has no actions; skipping turn.");
                onComplete?.Invoke();
                return;
            }

            var action = available[0];
            var impl = actionCatalog.Resolve(action);
            if (impl == null)
            {
                BattleLogger.Warn("Enemy", $"Action {action.id} missing implementation; skipping turn.");
                onComplete?.Invoke();
                return;
            }

            if (!impl.CanExecute(enemy, enemyContext, 0))
            {
                BattleLogger.Warn("Enemy", $"Action {action.id} cannot execute; skipping turn.");
                onComplete?.Invoke();
                return;
            }

            if (impl.CostSP > 0 && !enemy.SpendSP(impl.CostSP))
            {
                BattleLogger.Warn("Enemy", $"Not enough SP for {action.id}; skipping turn.");
                onComplete?.Invoke();
                return;
            }

            if (impl.CostCP > 0 && !enemy.SpendCP(impl.CostCP))
            {
                BattleLogger.Warn("Enemy", $"Not enough CP for {action.id}; skipping turn.");
                onComplete?.Invoke();
                return;
            }

            BattleLogger.Log("Enemy", $"Executing {action.id}");

            impl.Execute(enemy, enemyContext, 0, null, () =>
            {
                onComplete?.Invoke();
            });
        }

        private void HandlePostEnemyTurn()
        {
            if (enemy == null || player == null)
            {
                BattleLogger.Error("BattleManager", "Combatants missing after enemy turn.");
                state.Set(BattleState.Defeat);
                return;
            }

            if (enemy.IsDead())
            {
                state.Set(BattleState.Victory);
                return;
            }

            if (player.IsDead())
            {
                state.Set(BattleState.Defeat);
                return;
            }

            state.Set(BattleState.AwaitingAction);
            RequestPlayerAction();
        }

        private void ExecuteAutoFallback()
        {
            var fallback = actionCatalog.Fallback(player, context);
            BattleLogger.Log("Fallback", $"Executing fallback action {fallback.id}");
            var profile = ResolveChargeProfile(fallback) ?? ChargeProfile.CreateRuntimeDefault();
            ExecuteAction(new BattleSelection(fallback, 0, profile, null));
        }

        private ChargeProfile ResolveChargeProfile(BattleActionData action)
        {
            if (actionCatalog == null || action == null)
            {
                return null;
            }

            var impl = actionCatalog.Resolve(action);
            return impl != null ? impl.ChargeProfile : null;
        }

        public void SetRuntimeInputProvider(IBattleInputProvider provider)
        {
            inputProvider = provider ?? ResolveProvider();
        }

        public void SetTimedHitRunner(ITimedHitRunner runner) => timedHitRunner = runner;

        private async void RunPlayerActionPipeline(BattleSelection selection, IAction implementation, int cpBefore)
        {
            try
            {
                var pipeline = actionPipelineFactory?.CreatePipeline(selection.Action, implementation)
                               ?? new DefaultActionPipelineFactory(this).CreatePipeline(selection.Action, implementation);

                var actionContext = new ActionContext(
                    this,
                    player,
                    context?.Enemy,
                    selection.Action,
                    implementation,
                    context,
                    selection);

                await pipeline.ExecuteAsync(actionContext);

                var resolvedSelection = new BattleSelection(
                    selection.Action,
                    selection.CpCharge,
                    selection.ChargeProfile,
                    selection.TimedHitProfile,
                    actionContext.TimedResult);

                BattleLogger.Log("Resolve", "Enemy turn resolving...");
                waitingForEnemyAnimation = true;
                int cpAfter = player != null ? player.CurrentCP : 0;
                OnPlayerActionResolved?.Invoke(resolvedSelection, cpBefore, cpAfter);
                QueueEnemyTurn(() => ExecuteEnemyTurn(HandlePostEnemyTurn));
                TryExecutePendingEnemyTurn();
            }
            catch (Exception ex)
            {
                BattleLogger.Error("BattleManager", $"Action {selection.Action?.id ?? "unknown"} threw exception: {ex}");
                ExecuteAutoFallback();
            }
        }

        private void HandleAnimationLockChanged(bool locked)
        {
            animationLocked = locked;

            if (!animationLocked)
            {
                TryExecutePendingPlayerAction();
                TryExecutePendingEnemyTurn();
            }
        }

        private void HandleAnimationStageCompleted(BattleAnimationStage stage)
        {
            switch (stage)
            {
                case BattleAnimationStage.PlayerAttack:
                    waitingForPlayerAnimation = false;
                    TryExecutePendingPlayerAction();
                    break;
                case BattleAnimationStage.EnemyAttack:
                    waitingForEnemyAnimation = false;
                    TryExecutePendingEnemyTurn();
                    break;
            }
        }
    }
}






