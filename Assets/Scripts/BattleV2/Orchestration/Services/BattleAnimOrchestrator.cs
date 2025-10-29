using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BattleV2.Core;
using BattleV2.Orchestration.Events;
using BattleV2.Orchestration.Services.Animation;
using BattleV2.Providers;
using UnityEngine;

namespace BattleV2.Orchestration.Services
{
    public interface IBattleAnimOrchestrator
    {
        Task PlayAsync(ActionPlaybackRequest request);
    }

    /// <summary>
    /// Controls basic action timeline timing so gameplay can wait on presentation completion.
    /// </summary>
    public sealed class BattleAnimOrchestrator : IBattleAnimOrchestrator
    {
        private readonly IBattleEventBus eventBus;
        private readonly BattleTimingProfile timing;

        public BattleAnimOrchestrator(IBattleEventBus eventBus, BattleTimingProfile timing)
        {
            this.eventBus = eventBus;
            this.timing = timing;
        }

        public async Task PlayAsync(ActionPlaybackRequest request)
        {
            if (request.Actor == null)
            {
                return;
            }

            eventBus?.Publish(new ActionStartedEvent(request.Actor, request.Selection, request.Targets));

            float actorSpeed = Mathf.Max(0.01f, request.ActorSpeed);
            float averageSpeed = Mathf.Max(0.01f, request.AverageSpeed);
            float speedScale = Mathf.Clamp(averageSpeed / actorSpeed, timing.SpeedScaleMin, timing.SpeedScaleMax);

            float actionDelay = timing.BaseActionTime * speedScale;
            if (actionDelay > 0f)
            {
                await Task.Delay(TimeSpan.FromSeconds(actionDelay));
            }

            var targets = request.Targets;
            int total = targets.Count;
            if (total == 0)
            {
                return;
            }

            for (int i = 0; i < total; i++)
            {
                var target = targets[i];
                eventBus?.Publish(new AttackFrameEvent(request.Actor, target, request.Selection.Action, i, total));

                if (i < total - 1)
                {
                    float perTargetDelay = timing.PerTargetDelay * speedScale;
                    if (perTargetDelay > 0f)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(perTargetDelay));
                    }
                }
            }
        }
    }

    public readonly struct ActionPlaybackRequest
    {
        public ActionPlaybackRequest(
            CombatantState actor,
            BattleSelection selection,
            IReadOnlyList<CombatantState> targets,
            float averageSpeed)
        {
            Actor = actor;
            Selection = selection;
            Targets = targets ?? Array.Empty<CombatantState>();
            AverageSpeed = averageSpeed;
            ActorSpeed = actor != null ? actor.FinalStats.Speed : 1f;
        }

        public ActionPlaybackRequest(
            CombatantState actor,
            BattleSelection selection,
            IReadOnlyList<CombatantState> targets)
            : this(actor, selection, targets, 1f)
        {
        }

        public CombatantState Actor { get; }
        public BattleSelection Selection { get; }
        public IReadOnlyList<CombatantState> Targets { get; }
        public float AverageSpeed { get; }
        public float ActorSpeed { get; }
    }
}
