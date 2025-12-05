using BattleV2.Actions;
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

        public ActionJudgment WithTimedGrade(TimedGrade grade) =>
            new ActionJudgment(CpSpent, grade, Audience, Shape, RngSeed, ActionId, SourceActorId, true);

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
