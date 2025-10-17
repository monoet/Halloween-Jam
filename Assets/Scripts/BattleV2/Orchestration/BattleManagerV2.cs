using System;
using BattleV2.Actions;
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
        [SerializeField] private ScriptableObject inputProviderBehaviour;

        [Header("Entities")]
        [SerializeField] private CombatantState player;
        [SerializeField] private CombatantState enemy;

        private IBattleInputProvider inputProvider;
        private CombatContext context;

        public ActionData LastExecutedAction { get; private set; }

        private void Awake()
        {
            if (config != null)
            {
                if (actionCatalog == null)
                {
                    actionCatalog = config.actionCatalog;
                }

                if (inputProviderBehaviour == null)
                {
                    inputProviderBehaviour = config.inputProvider;
                }
            }

            inputProvider = inputProviderBehaviour as IBattleInputProvider;

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
                Context = context
            };

            inputProvider.RequestAction(actionContext, ExecuteAction, ExecuteAutoFallback);
        }

        private void ExecuteAction(ActionData selected)
        {
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

            if (!impl.CanExecute(player, context))
            {
                BattleLogger.Warn("BattleManager", $"Action {selected.id} cannot execute; using fallback.");
                ExecuteAutoFallback();
                return;
            }

            LastExecutedAction = selected;
            BattleLogger.Log("Execute", $"Action {selected.id} starting.");
            state.Set(BattleState.Resolving);

            try
            {
                impl.Execute(player, context, () =>
                {
                    BattleLogger.Log("Resolve", "Enemy turn resolving...");
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

            if (!impl.CanExecute(enemy, enemyContext))
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

            impl.Execute(enemy, enemyContext, () =>
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
            ExecuteAction(fallback);
        }
    }
}
