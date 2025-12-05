using BattleV2.Actions;
using BattleV2.Charge;
using BattleV2.Core;
using BattleV2.Providers;
using BattleV2.Targeting;

namespace BattleV2.Execution
{
    public enum TimedGrade
    {
        None = 0,
        Fail = 1,
        Success = 2,
        Perfect = 3
    }

    /// <summary>
    /// Immutable action-level judgment captured at commit time.
    /// </summary>
    public readonly struct ActionJudgment
    {
        public ActionJudgment(
            int cpSpent,
            TimedGrade timedGrade,
            TargetAudience audience,
            TargetShape shape,
            int rngSeed,
            string actionId,
            int sourceActorId,
            bool hasValue)
        {
            CpSpent = cpSpent;
            TimedGrade = timedGrade;
            Audience = audience;
            Shape = shape;
            RngSeed = rngSeed;
            ActionId = actionId;
            SourceActorId = sourceActorId;
            HasValue = hasValue;
        }

        public int CpSpent { get; }
        public TimedGrade TimedGrade { get; }
        public TargetAudience Audience { get; }
        public TargetShape Shape { get; }
        public int RngSeed { get; }
        public string ActionId { get; }
        public int SourceActorId { get; }
        public bool HasValue { get; }

        public ActionJudgment WithTimedGrade(TimedGrade grade)
        {
            // Atomic: once a timed grade is set, do not overwrite.
            if (TimedGrade != TimedGrade.None)
            {
                return this;
            }

            return new ActionJudgment(CpSpent, grade, Audience, Shape, RngSeed, ActionId, SourceActorId, true);
        }

        public static ActionJudgment FromSelection(BattleSelection selection, CombatantState actor, int cpSpent, int selectionSeed)
        {
            var action = selection.Action;
            return new ActionJudgment(
                cpSpent,
                TimedGrade.None,
                action != null ? action.targetAudience : TargetAudience.Enemies,
                action != null ? action.targetShape : TargetShape.Single,
                selectionSeed,
                action != null ? action.id : null,
                actor != null ? actor.GetInstanceID() : 0,
                true);
        }

        // Contract: TimedGrade applies to the entire action, not per-target.
        public static TimedGrade ResolveTimedGrade(TimedHitResult? timedResult)
        {
            if (!timedResult.HasValue)
            {
                return TimedGrade.None;
            }

            var result = timedResult.Value;
            if (result.Cancelled)
            {
                return TimedGrade.Fail;
            }

            if (result.TotalHits <= 0)
            {
                return TimedGrade.None;
            }

            if (result.HitsSucceeded <= 0)
            {
                return TimedGrade.Fail;
            }

            if (result.HitsSucceeded >= result.TotalHits)
            {
                return TimedGrade.Perfect;
            }

            return TimedGrade.Success;
        }
    }

    /// <summary>
    /// Immutable per-target judgment derived from ActionJudgment.
    /// </summary>
    public readonly struct TargetJudgment
    {
        public TargetJudgment(int index, int targetId, int rngSeed, bool hasValue)
        {
            Index = index;
            TargetId = targetId;
            RngSeed = rngSeed;
            HasValue = hasValue;
        }

        public int Index { get; }
        public int TargetId { get; }
        public int RngSeed { get; }
        public bool HasValue { get; }

        public static TargetJudgment From(ActionJudgment actionJudgment, int index, CombatantState target)
        {
            int targetId = target != null ? target.GetInstanceID() : 0;
            int seed = actionJudgment.RngSeed ^ targetId ^ index;
            return new TargetJudgment(index, targetId, seed, actionJudgment.HasValue);
        }
    }
}
