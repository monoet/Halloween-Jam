using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BattleV2.Actions;
using BattleV2.Core;
using BattleV2.Charge;
using BattleV2.Orchestration.Events;
using BattleV2.Orchestration;
using BattleV2.Providers;
using BattleV2.Targeting;
using BattleV2.Execution;
using BattleV2.Marks;
using UnityEngine;

namespace BattleV2.Orchestration.Services
{
    public interface ITriggeredEffectsService
    {
        void Schedule(
            int executionId,
            CombatantState origin,
            BattleSelection selection,
            TimedHitResult? timedResult,
            ExecutionSnapshot snapshot,
            CombatContext context);

        void Clear();
    }

    /// <summary>
    /// Cola secuencial de efectos encadenados que reutiliza el <see cref="IActionPipeline"/>.
    /// </summary>
    public sealed class TriggeredEffectsService : ITriggeredEffectsService
    {
        private readonly BattleManagerV2 manager;
        private readonly IActionPipeline actionPipeline;
        private readonly ActionCatalog actionCatalog;
        private readonly IBattleEventBus eventBus;
        private readonly MarkInteractionProcessor markProcessor;
        private readonly Queue<TriggeredEffectRequest> queue = new();
        private readonly object gate = new();
        private bool processing;
        private bool cancellationRequested;

        public TriggeredEffectsService(
            BattleManagerV2 manager,
            IActionPipeline actionPipeline,
            ActionCatalog actionCatalog,
            IBattleEventBus eventBus,
            MarkInteractionProcessor markProcessor)
        {
            this.manager = manager;
            this.actionPipeline = actionPipeline;
            this.actionCatalog = actionCatalog;
            this.eventBus = eventBus;
            this.markProcessor = markProcessor;
        }

        public void Schedule(
            int executionId,
            CombatantState origin,
            BattleSelection selection,
            TimedHitResult? timedResult,
            ExecutionSnapshot snapshot,
            CombatContext context)
        {
            if (origin == null || selection.Action == null)
            {
                return;
            }

            var snapshotTargets = snapshot.Targets ?? Array.Empty<CombatantState>();
            var effectSelection = selection.WithTimedResult(timedResult);
            var request = new TriggeredEffectRequest(
                executionId,
                origin,
                selection.Action,
                effectSelection,
                snapshot,
                context);

            Enqueue(request);
        }

        private void Enqueue(TriggeredEffectRequest request)
        {
            if (request.Action == null || request.Origin == null)
            {
                return;
            }

            lock (gate)
            {
                if (!request.Origin.IsAlive)
                {
                    return;
                }

                queue.Enqueue(request);
                if (processing)
                {
                    return;
                }

                processing = true;
                cancellationRequested = false;
            }

            _ = ProcessQueueAsync();
        }

        public void Clear()
        {
            lock (gate)
            {
                queue.Clear();
                if (processing)
                {
                    cancellationRequested = true;
                }
                else
                {
                    cancellationRequested = false;
                }
            }
        }

        private async Task ProcessQueueAsync()
        {
            while (true)
            {
                TriggeredEffectRequest request;

                lock (gate)
                {
                    if (cancellationRequested)
                    {
                        queue.Clear();
                        cancellationRequested = false;
                        processing = false;
                        return;
                    }

                    if (queue.Count == 0)
                    {
                        processing = false;
                        return;
                    }

                    request = queue.Dequeue();
                }

                await ExecuteTriggeredEffectAsync(request);
            }
        }

        private async Task ExecuteTriggeredEffectAsync(TriggeredEffectRequest request)
        {
            var implementation = actionCatalog != null ? actionCatalog.Resolve(request.Action) : null;
            if (implementation == null)
            {
                return;
            }

            if (request.Origin == null || !request.Origin.IsAlive)
            {
                return;
            }

            var targets = request.Snapshot.Targets ?? Array.Empty<CombatantState>();
            CombatantState primaryTarget = null;
            for (int i = 0; i < targets.Count; i++)
            {
                var candidate = targets[i];
                if (candidate == null || !candidate.IsAlive)
                {
                    continue;
                }

                primaryTarget = candidate;
                break;
            }

            if (primaryTarget == null && targets.Count > 0)
            {
                primaryTarget = targets[0];
            }

            primaryTarget ??= request.Context?.Enemy ?? request.Origin;

            var services = request.Context?.Services ?? new BattleServices();
            var catalog = request.Context?.Catalog ?? actionCatalog;

            var effectContext = new CombatContext(
                request.Origin,
                primaryTarget,
                ResolveRuntime(request.Origin, request.Origin?.CharacterRuntime),
                ResolveRuntime(primaryTarget, primaryTarget?.CharacterRuntime),
                services,
                catalog);

            var selection = request.Selection;
            int judgmentSeed = System.HashCode.Combine(request.Origin != null ? request.Origin.GetInstanceID() : 0, selection.Action != null ? selection.Action.id.GetHashCode() : 0, targets.Count);
            var resourcesPre = ResourceSnapshot.FromCombatant(request.Origin);
            var resourcesPost = ResourceSnapshot.FromCombatant(request.Origin);
            var judgment = ActionJudgment.FromSelection(selection, request.Origin, selection.CpCharge, judgmentSeed, resourcesPre, resourcesPost);
            bool resourcesCharged = false;

            var actionRequest = new ActionRequest(
                manager,
                request.Origin,
                targets,
                selection,
                implementation,
                effectContext,
                judgment);

            try
            {
                var result = await actionPipeline.Run(actionRequest);
                resourcesPost = ResourceSnapshot.FromCombatant(request.Origin);
                if (resourcesCharged)
                {
#if UNITY_EDITOR
                    Debug.Assert(false, $"[CP/SP] Duplicate charge: action={selection.Action?.id ?? "null"} actor={request.Origin?.name ?? "(null)"}");
#endif
                    Debug.LogWarning($"[CP/SP] Duplicate charge detected: action={selection.Action?.id ?? "null"} actor={request.Origin?.name ?? "(null)"}");
                }
                resourcesCharged = true;
                var judgmentWithCosts = judgment.WithPostCost(resourcesPost);
                var timedGrade = ActionJudgment.ResolveTimedGrade(result.TimedResult);
                var finalJudgment = judgmentWithCosts.WithTimedGrade(timedGrade);
                var selectionWithResult = selection.WithTimedResult(result.TimedResult);
                markProcessor?.Process(request.Origin, selectionWithResult, finalJudgment, targets, request.ExecutionId);
                eventBus?.Publish(new ActionCompletedEvent(request.ExecutionId, request.Origin, selectionWithResult, targets, true, finalJudgment));
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[TriggeredEffectsService] Failed to run triggered effect {request.Action.id}: {ex}");
            }
        }

        private static CharacterRuntime ResolveRuntime(CombatantState combatant, CharacterRuntime fallback)
        {
            if (fallback != null)
            {
                return fallback;
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
    }

    public readonly struct TriggeredEffectRequest
    {
        public TriggeredEffectRequest(
            int executionId,
            CombatantState origin,
            BattleActionData action,
            BattleSelection selection,
            ExecutionSnapshot snapshot,
            CombatContext context)
        {
            ExecutionId = executionId;
            Origin = origin;
            Action = action;
            Selection = selection;
            Snapshot = snapshot;
            Context = context;
        }

        public int ExecutionId { get; }
        public CombatantState Origin { get; }
        public BattleActionData Action { get; }
        public BattleSelection Selection { get; }
        public ExecutionSnapshot Snapshot { get; }
        public CombatContext Context { get; }
    }
}
