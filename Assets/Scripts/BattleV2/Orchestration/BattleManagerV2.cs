using System;
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

        private IBattleInputProvider inputProvider;
        private CombatContext context;
        private bool animationLocked;
        private Action pendingEnemyTurn;
        private BattleSelection pendingPlayerSelection;
        private IAction pendingPlayerAction;
        private int pendingPlayerCpBefore;
        private bool waitingForPlayerAnimation;
        private bool waitingForEnemyAnimation;
        private IActionPipelineFactory actionPipelineFactory;
        private ITimedHitRunner timedHitRunner;

        public BattleActionData LastExecutedAction { get; private set; }
        public ITimedHitRunner TimedHitRunner => timedHitRunner ?? InstantTimedHitRunner.Shared;
        public event Action<BattleSelection, int> OnPlayerActionSelected;
        public event Action<BattleSelection, int, int> OnPlayerActionResolved;

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
            playerRuntime = ResolveRuntimeReference(player, playerRuntime);
            enemyRuntime = ResolveRuntimeReference(enemy, enemyRuntime);

            if (player != null)
            {
                if (playerRuntime != null)
                {
                    player.SetCharacterRuntime(playerRuntime, initialize: true, preserveVitals: preservePlayerVitals);
                    playerRuntime = player.CharacterRuntime;
                }
                else
                {
                    BattleLogger.Warn("BattleManager", "Player runtime missing; using fallback vitals.");
                    player.InitializeFrom(null, preservePlayerVitals);
                }
            }

            if (enemy != null)
            {
                if (enemyRuntime != null)
                {
                    enemy.SetCharacterRuntime(enemyRuntime, initialize: true, preserveVitals: preserveEnemyVitals);
                    enemyRuntime = enemy.CharacterRuntime;
                }
                else
                {
                    BattleLogger.Warn("BattleManager", "Enemy runtime missing; using fallback vitals.");
                    enemy.InitializeFrom(null, preserveEnemyVitals);
                }
            }
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

            OnPlayerActionSelected?.Invoke(selection, pendingPlayerCpBefore);
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

            if (waitingForPlayerAnimation)
            {
                return;
            }

            waitingForPlayerAnimation = false;
            var selection = pendingPlayerSelection;
            var impl = pendingPlayerAction;
            int cpBefore = pendingPlayerCpBefore;

            pendingPlayerAction = null;
            pendingPlayerSelection = default;
            pendingPlayerCpBefore = 0;

            RunPlayerActionPipeline(selection, impl, cpBefore);
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






