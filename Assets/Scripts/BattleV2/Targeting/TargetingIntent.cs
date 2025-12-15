using BattleV2.Actions;

namespace BattleV2.Targeting
{
    /// <summary>
    /// Minimal DTO to describe targeting side/scope without coupling to full action data.
    /// </summary>
    public readonly struct TargetingIntent
    {
        public TargetingIntent(TargetAudience audience, TargetShape shape, bool requiresTarget = true)
        {
            Audience = audience;
            Shape = shape;
            RequiresTarget = requiresTarget;
            HasValue = true;
        }

        public TargetAudience Audience { get; }
        public TargetShape Shape { get; }
        public bool RequiresTarget { get; }
        public bool HasValue { get; }

        public static TargetingIntent FromAction(BattleActionData action)
        {
            if (action == null)
            {
                return default;
            }

            return new TargetingIntent(
                action.targetAudience,
                action.targetShape,
                action.requiresTarget);
        }
    }
}
