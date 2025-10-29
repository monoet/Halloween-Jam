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
using UnityEngine;

namespace BattleV2.Orchestration.Services
{
    public interface ITriggeredEffectsService
    {
        void Schedule(
            CombatantState origin,
            BattleSelection selection,
            TimedHitResult? timedResult,
            IReadOnlyList<CombatantState> targets,
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
        private readonly Queue<TriggeredEffectRequest> queue = new();
        private readonly object gate = new();
        private bool processing;
        private bool cancellationRequested;

        public TriggeredEffectsService(
            BattleManagerV2 manager,
            IActionPipeline actionPipeline,
            ActionCatalog actionCatalog,
            IBattleEventBus eventBus)
        {
            this.manager = manager;
            this.actionPipeline = actionPipeline;
            this.actionCatalog = actionCatalog;
            this.eventBus = eventBus;
        }

        public void Schedule(
            CombatantState origin,
            BattleSelection selection,
            TimedHitResult? timedResult,
            IReadOnlyList<CombatantState> targets,
            CombatContext context)
        {
            if (origin == null || selection.Action == null)
            {
                return;
            }

            targets ??= Array.Empty<CombatantState>();
            if (targets.Count == 0)
            {
                return;
            }

            var effectSelection = selection.WithTimedResult(timedResult);
            var request = new TriggeredEffectRequest(
                origin,
                selection.Action,
                effectSelection,
                targets,
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

            var targets = request.Targets ?? Array.Empty<CombatantState>();
            if (targets.Count == 0)
            {
                return;
            }

            var primaryTarget = targets[0];
            if (primaryTarget == null || !primaryTarget.IsAlive)
            {
                return;
            }

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
            var actionRequest = new ActionRequest(
                manager,
                request.Origin,
                targets,
                selection,
                implementation,
                effectContext);

            try
            {
                var result = await actionPipeline.Run(actionRequest);
                eventBus?.Publish(new ActionCompletedEvent(request.Origin, selection.WithTimedResult(result.TimedResult), request.Targets, true));
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
            CombatantState origin,
            BattleActionData action,
            BattleSelection selection,
            IReadOnlyList<CombatantState> targets,
            CombatContext context)
        {
            Origin = origin;
            Action = action;
            Selection = selection;
            Targets = targets ?? Array.Empty<CombatantState>();
            Context = context;
        }

        public CombatantState Origin { get; }
        public BattleActionData Action { get; }
        public BattleSelection Selection { get; }
        public IReadOnlyList<CombatantState> Targets { get; }
        public CombatContext Context { get; }
    }
}
