using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BattleV2.Core;
using BattleV2.Providers;
using BattleV2.Execution.TimedHits;

namespace BattleV2.AnimationSystem
{
    /// <summary>
    /// Entry point for the data-driven JRPG animation pipeline.
    /// Implementations must execute the request and resolve when
    /// the orchestration is complete.
    /// </summary>
    public interface IAnimationOrchestrator
    {
        Task PlayAsync(AnimationRequest request, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Canonical payload for animation executions. Mirrors the
    /// existing battle pipeline contract so legacy adapters can
    /// participate until the new system is ready.
    /// </summary>
    public readonly struct AnimationRequest
    {
        public AnimationRequest(
            CombatantState actor,
            BattleSelection selection,
            IReadOnlyList<CombatantState> targets,
            float averageSpeed,
            ITimedHitRunner timedHitRunner = null)
        {
            Actor = actor;
            Selection = selection;
            Targets = targets ?? System.Array.Empty<CombatantState>();
            AverageSpeed = averageSpeed <= 0f ? 1f : averageSpeed;
            ActorSpeed = actor != null ? actor.FinalStats.Speed : 1f;
            TimedHitRunner = timedHitRunner;
        }

        public AnimationRequest(
            CombatantState actor,
            BattleSelection selection,
            IReadOnlyList<CombatantState> targets,
            ITimedHitRunner timedHitRunner = null)
            : this(actor, selection, targets, 1f, timedHitRunner)
        {
        }

        public CombatantState Actor { get; }
        public BattleSelection Selection { get; }
        public IReadOnlyList<CombatantState> Targets { get; }
        public float AverageSpeed { get; }
        public float ActorSpeed { get; }
        public ITimedHitRunner TimedHitRunner { get; }
    }
}
