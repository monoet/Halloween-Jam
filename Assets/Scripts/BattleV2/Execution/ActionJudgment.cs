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
            bool hasValue,
            ResourceSnapshot resourcesPreCost,
            ResourceSnapshot resourcesPostCost)
        {
            CpSpent = cpSpent;
            TimedGrade = timedGrade;
            Audience = audience;
            Shape = shape;
            RngSeed = rngSeed;
            ActionId = actionId;
            SourceActorId = sourceActorId;
            HasValue = hasValue;
            ResourcesPreCost = resourcesPreCost;
            ResourcesPostCost = resourcesPostCost;
        }

        public int CpSpent { get; }
        public TimedGrade TimedGrade { get; }
        public TargetAudience Audience { get; }
        public TargetShape Shape { get; }
        public int RngSeed { get; }
        public string ActionId { get; }
        public int SourceActorId { get; }
        public bool HasValue { get; }
        public ResourceSnapshot ResourcesPreCost { get; }
        public ResourceSnapshot ResourcesPostCost { get; }

        public ActionJudgment WithPostCost(ResourceSnapshot postCost)
        {
            return new ActionJudgment(CpSpent, TimedGrade, Audience, Shape, RngSeed, ActionId, SourceActorId, HasValue, ResourcesPreCost, postCost);
        }

        public ActionJudgment WithTimedGrade(TimedGrade grade)
        {
            // Atomic: once a timed grade is set, do not overwrite.
            if (TimedGrade != TimedGrade.None)
            {
                return this;
            }

            return new ActionJudgment(CpSpent, grade, Audience, Shape, RngSeed, ActionId, SourceActorId, true, ResourcesPreCost, ResourcesPostCost);
        }

        public static ActionJudgment FromSelection(BattleSelection selection, CombatantState actor, int cpSpent, int selectionSeed, ResourceSnapshot resourcesPreCost, ResourceSnapshot resourcesPostCost)
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
                true,
                resourcesPreCost,
                resourcesPostCost);
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

    /// <summary>
    /// Snapshot of resource state at judgment time.
    /// </summary>
    public readonly struct ResourceSnapshot
    {
        public ResourceSnapshot(int cpCurrent, int spCurrent, int cpMax, int spMax, bool hasValue = true)
        {
            CpCurrent = cpCurrent;
            SpCurrent = spCurrent;
            CpMax = cpMax;
            SpMax = spMax;
            HasValue = hasValue;
        }

        public int CpCurrent { get; }
        public int SpCurrent { get; }
        public int CpMax { get; }
        public int SpMax { get; }
        public bool HasValue { get; }

        public static ResourceSnapshot FromCombatant(CombatantState combatant)
        {
            if (combatant == null)
            {
                return default;
            }

            return new ResourceSnapshot(
                combatant.CurrentCP,
                combatant.CurrentSP,
                combatant.MaxCP,
                combatant.MaxSP,
                hasValue: true);
        }
    }
}
