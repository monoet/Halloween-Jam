using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BattleV2.Actions;
using BattleV2.Charge;
using BattleV2.Core;
using BattleV2.Execution;
using BattleV2.Execution.TimedHits;
using BattleV2.Providers;
using UnityEngine;

namespace BattleV2.Orchestration.Services
{
    public readonly struct ActionRequest
    {
        public ActionRequest(
            BattleManagerV2 manager,
            CombatantState actor,
            IReadOnlyList<CombatantState> targets,
            BattleSelection selection,
            IAction implementation,
            CombatContext combatContext)
        {
            Manager = manager;
            Actor = actor;
            Targets = targets;
            Selection = selection;
            Implementation = implementation;
            CombatContext = combatContext;
        }

        public BattleManagerV2 Manager { get; }
        public CombatantState Actor { get; }
        public IReadOnlyList<CombatantState> Targets { get; }
        public BattleSelection Selection { get; }
        public IAction Implementation { get; }
        public CombatContext CombatContext { get; }

        public CombatantState PrimaryTarget =>
            Targets != null && Targets.Count > 0 ? Targets[0] : CombatContext?.Enemy;
    }

    public readonly struct ActionResult
    {
        private ActionResult(bool success, TimedHitResult? timedResult, int comboPointsAwarded)
        {
            Success = success;
            TimedResult = timedResult;
            ComboPointsAwarded = comboPointsAwarded;
        }

        public bool Success { get; }
        public TimedHitResult? TimedResult { get; }
        public int ComboPointsAwarded { get; }

        public static ActionResult From(TimedHitResult? timedResult, int comboPointsAwarded) =>
            new(true, timedResult, comboPointsAwarded);

        public static ActionResult Failure => new(false, null, 0);
    }

    public interface IActionPipeline
    {
        Task<ActionResult> Run(ActionRequest request);
    }

    /// <summary>
    /// Columna vertebral para acciones de combate. Actualmente delega en el pipeline legacy mientras migramos.
    /// </summary>
    public sealed class OrchestrationActionPipeline : IActionPipeline
    {
        private readonly IBattleEventBus eventBus;

        public OrchestrationActionPipeline(IBattleEventBus eventBus)
        {
            this.eventBus = eventBus;
        }

        public Task<ActionResult> Run(ActionRequest request)
        {
            if (request.Manager == null || request.Actor == null || request.Selection.Action == null || request.Implementation == null)
            {
                return Task.FromResult(ActionResult.Failure);
            }

            return RunLegacyPipelineAsync(request);
        }

        private async Task<ActionResult> RunLegacyPipelineAsync(ActionRequest request)
        {
            var pipelineFactory = new DefaultActionPipelineFactory(request.Manager);
            var pipeline = pipelineFactory.CreatePipeline(request.Selection.Action, request.Implementation);

            CombatantState effectiveTarget = request.PrimaryTarget ?? request.CombatContext?.Enemy;
            var combatContext = EnsureTargetContext(request.CombatContext, effectiveTarget);

            var actionContext = new Execution.ActionContext(
                request.Manager,
                request.Actor,
                effectiveTarget,
                request.Selection.Action,
                request.Implementation,
                combatContext,
                request.Selection);

            await pipeline.ExecuteAsync(actionContext);
            return ActionResult.From(actionContext.TimedResult, actionContext.ComboPointsAwarded);
        }

        private static CombatContext EnsureTargetContext(CombatContext context, CombatantState target)
        {
            if (context == null || target == null || context.Enemy == target)
            {
                return context;
            }

            var runtime = ResolveRuntime(target);
            return context.WithEnemy(target, runtime);
        }

        private static CharacterRuntime ResolveRuntime(CombatantState combatant)
        {
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
    }
}
