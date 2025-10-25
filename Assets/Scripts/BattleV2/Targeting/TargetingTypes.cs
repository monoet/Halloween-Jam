using System.Collections.Generic;
using BattleV2.Actions;
using BattleV2.Core;

namespace BattleV2.Targeting
{
    public enum TargetAudience
    {
        Self,
        Allies,
        Enemies
    }

    public enum TargetShape
    {
        Single,
        All
    }

    public enum TargetSourceType
    {
        Manual,
        Auto,
        Trigger
    }

    public readonly struct TargetQuery
    {
        public TargetQuery(TargetAudience audience, TargetShape shape)
        {
            Audience = audience;
            Shape = shape;
        }

        public TargetAudience Audience { get; }
        public TargetShape Shape { get; }

        public static TargetQuery SelfSingle => new TargetQuery(TargetAudience.Self, TargetShape.Single);
        public static TargetQuery EnemiesSingle => new TargetQuery(TargetAudience.Enemies, TargetShape.Single);
        public static TargetQuery AlliesSingle => new TargetQuery(TargetAudience.Allies, TargetShape.Single);
    }

    public readonly struct TargetContext
    {
        public TargetContext(
            CombatantState origin,
            BattleActionData action,
            TargetQuery query,
            TargetSourceType sourceType,
            IReadOnlyList<CombatantState> allies,
            IReadOnlyList<CombatantState> enemies)
        {
            Origin = origin;
            Action = action;
            Query = query;
            SourceType = sourceType;
            Allies = allies;
            Enemies = enemies;
        }

        public CombatantState Origin { get; }
        public BattleActionData Action { get; }
        public TargetQuery Query { get; }
        public TargetSourceType SourceType { get; }
        public IReadOnlyList<CombatantState> Allies { get; }
        public IReadOnlyList<CombatantState> Enemies { get; }
    }
}
