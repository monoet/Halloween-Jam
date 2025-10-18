using System;
using BattleV2.Actions;
using BattleV2.Charge;
using BattleV2.Core;
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
        [SerializeField] private CombatantState enemy;

        private IBattleInputProvider inputProvider;
        private CombatContext context;

        public BattleActionData LastExecutedAction { get; private set; }
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

            context = new CombatContext(
                player,
                enemy,
                config != null ? config.services : new BattleServices(),
                actionCatalog);
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

        public void StartBattle()
        {
            state.ResetToIdle();
            state.Set(BattleState.AwaitingAction);
            RequestPlayerAction();
        }

        public void ResetBattle()
        {
            if (context == null)
            {
                context = new CombatContext(player, enemy, config != null ? config.services : new BattleServices(), actionCatalog);
            }
            else
            {
                context = new CombatContext(player, enemy, context.Services, actionCatalog);
            }

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
                Enemy = enemy,
                AvailableActions = available,
                Context = context,
                MaxCpCharge = player != null ? player.CurrentCP : 0
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
            int cpBefore = player.CurrentCP;
            OnPlayerActionSelected?.Invoke(selection, cpBefore);

            try
            {
                impl.Execute(player, context, selection.CpCharge, () =>
                {
                    BattleLogger.Log("Resolve", "Enemy turn resolving...");
                    int cpAfter = player.CurrentCP;
                    OnPlayerActionResolved?.Invoke(selection, cpBefore, cpAfter);
                    ExecuteEnemyTurn(HandlePostEnemyTurn);
                });
            }
            catch (Exception ex)
            {
                BattleLogger.Error("BattleManager", $"Action {selected.id} threw exception: {ex}");
                ExecuteAutoFallback();
            }
        }

        private void ExecuteEnemyTurn(Action onComplete)
        {
            var enemyContext = new CombatContext(enemy, player, context.Services, actionCatalog);
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

            impl.Execute(enemy, enemyContext, 0, () =>
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
    }
}
