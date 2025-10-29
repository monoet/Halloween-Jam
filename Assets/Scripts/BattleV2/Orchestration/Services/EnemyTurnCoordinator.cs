using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BattleV2.Actions;
using BattleV2.Core;
using BattleV2.Execution;
using BattleV2.Orchestration.Events;
using BattleV2.Providers;
using BattleV2.Targeting;
using UnityEngine;

namespace BattleV2.Orchestration.Services
{
    public interface IEnemyTurnCoordinator
    {
        Task ExecuteAsync(EnemyTurnContext context);
    }

    public readonly struct EnemyTurnContext
    {
        public EnemyTurnContext(
            BattleManagerV2 manager,
            CombatantState attacker,
            CombatantState player,
            CombatContext combatContext,
            IReadOnlyList<CombatantState> allies,
            IReadOnlyList<CombatantState> enemies,
            float averageSpeed,
            BattleStateController stateController,
            Action<CombatantState> advanceTurn,
            Action stopTurnService,
            Func<bool> tryResolveBattleEnd,
            Action refreshCombatContext)
        {
            Manager = manager;
            Attacker = attacker;
            Player = player;
            CombatContext = combatContext;
            Allies = allies ?? Array.Empty<CombatantState>();
            Enemies = enemies ?? Array.Empty<CombatantState>();
            AverageSpeed = averageSpeed;
            StateController = stateController;
            AdvanceTurn = advanceTurn ?? (_ => { });
            StopTurnService = stopTurnService ?? (() => { });
            TryResolveBattleEnd = tryResolveBattleEnd ?? (() => false);
            RefreshCombatContext = refreshCombatContext ?? (() => { });
        }

        public BattleManagerV2 Manager { get; }
        public CombatantState Attacker { get; }
        public CombatantState Player { get; }
        public CombatContext CombatContext { get; }
        public IReadOnlyList<CombatantState> Allies { get; }
        public IReadOnlyList<CombatantState> Enemies { get; }
        public float AverageSpeed { get; }
        public BattleStateController StateController { get; }
        public Action<CombatantState> AdvanceTurn { get; }
        public Action StopTurnService { get; }
        public Func<bool> TryResolveBattleEnd { get; }
        public Action RefreshCombatContext { get; }
    }

    public sealed class EnemyTurnCoordinator : IEnemyTurnCoordinator
    {
        private readonly ActionCatalog actionCatalog;
        private readonly ICombatantActionValidator actionValidator;
        private readonly ITargetingCoordinator targetingCoordinator;
        private readonly IActionPipeline actionPipeline;
        private readonly ITriggeredEffectsService triggeredEffects;
        private readonly IBattleAnimOrchestrator animOrchestrator;
        private readonly IBattleEventBus eventBus;

        public EnemyTurnCoordinator(
            ActionCatalog actionCatalog,
            ICombatantActionValidator actionValidator,
            ITargetingCoordinator targetingCoordinator,
            IActionPipeline actionPipeline,
            ITriggeredEffectsService triggeredEffects,
            IBattleAnimOrchestrator animOrchestrator,
            IBattleEventBus eventBus)
        {
            this.actionCatalog = actionCatalog;
            this.actionValidator = actionValidator;
            this.targetingCoordinator = targetingCoordinator;
            this.actionPipeline = actionPipeline;
            this.triggeredEffects = triggeredEffects;
            this.animOrchestrator = animOrchestrator;
            this.eventBus = eventBus;
        }

        public async Task ExecuteAsync(EnemyTurnContext context)
        {
            var attacker = context.Attacker;
            if (attacker == null || !attacker.IsAlive)
            {
                context.AdvanceTurn(attacker);
                context.StateController?.Set(BattleState.AwaitingAction);
                return;
            }

            context.StateController?.Set(BattleState.Resolving);

            var target = context.Player;
            if (target == null || target.IsDead())
            {
                context.StateController?.Set(BattleState.Defeat);
                context.StopTurnService();
                return;
            }

            var combatContext = context.CombatContext;
            var available = actionCatalog?.BuildAvailableFor(attacker, combatContext);
            if (available == null || available.Count == 0)
            {
                Debug.LogWarning($"[EnemyTurnCoordinator] Enemy {attacker.name} has no actions.");
                context.AdvanceTurn(attacker);
                context.StateController?.Set(BattleState.AwaitingAction);
                return;
            }

            BattleActionData actionData = null;
            IAction implementation = null;
            for (int i = 0; i < available.Count; i++)
            {
                var candidate = available[i];
                if (actionValidator.TryValidate(candidate, attacker, combatContext, 0, out implementation))
                {
                    actionData = candidate;
                    break;
                }
            }

            if (actionData == null || implementation == null)
            {
                Debug.LogWarning($"[EnemyTurnCoordinator] Enemy {attacker.name} has no valid actions.");
                context.AdvanceTurn(attacker);
                context.StateController?.Set(BattleState.AwaitingAction);
                return;
            }

            var selection = new BattleSelection(
                actionData,
                0,
                implementation.ChargeProfile,
                null);

            await RunEnemyActionAsync(
                context,
                attacker,
                selection,
                implementation);
        }

        private async Task RunEnemyActionAsync(
            EnemyTurnContext context,
            CombatantState attacker,
            BattleSelection selection,
            IAction implementation)
        {
            try
            {
                var resolution = await targetingCoordinator.ResolveAsync(
                    attacker,
                    selection.Action,
                    TargetSourceType.Auto,
                    context.Player,
                    context.Allies,
                    context.Enemies);

                if (resolution.Targets.Count == 0)
                {
                    context.AdvanceTurn(attacker);
                    context.StateController?.Set(BattleState.AwaitingAction);
                    return;
                }

                var enrichedSelection = selection.WithTargets(resolution.TargetSet);

                var playbackTask = animOrchestrator != null
                    ? animOrchestrator.PlayAsync(new ActionPlaybackRequest(attacker, enrichedSelection, resolution.Targets, context.AverageSpeed))
                    : Task.CompletedTask;

                var request = new ActionRequest(
                    context.Manager,
                    attacker,
                    resolution.Targets,
                    enrichedSelection,
                    implementation,
                    context.CombatContext);

                var result = await actionPipeline.Run(request);
                if (!result.Success)
                {
                    context.AdvanceTurn(attacker);
                    context.StateController?.Set(BattleState.AwaitingAction);
                    return;
                }

                triggeredEffects?.Schedule(
                    attacker,
                    enrichedSelection,
                    result.TimedResult,
                    resolution.Targets,
                    context.CombatContext);

                if (playbackTask != null)
                {
                    try
                    {
                        await playbackTask;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[EnemyTurnCoordinator] Enemy playback failed: {ex}");
                    }
                }

                context.RefreshCombatContext();

                bool battleEnded = context.TryResolveBattleEnd();
                eventBus?.Publish(new ActionCompletedEvent(attacker, enrichedSelection.WithTimedResult(result.TimedResult), resolution.Targets));

                if (battleEnded)
                {
                    return;
                }

                context.StateController?.Set(BattleState.AwaitingAction);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EnemyTurnCoordinator] Enemy action error: {ex}");
                context.AdvanceTurn(attacker);
                context.StateController?.Set(BattleState.AwaitingAction);
            }
        }
    }
}
