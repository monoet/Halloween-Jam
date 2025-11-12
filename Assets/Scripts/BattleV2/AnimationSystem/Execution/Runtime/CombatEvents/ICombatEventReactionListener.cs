namespace BattleV2.AnimationSystem.Execution.Runtime.CombatEvents
{
    public interface ICombatEventTweenListener
    {
        void PlayTween(string flagId, CombatEventContext context, TweenPreset preset);
    }

    public interface ICombatEventSfxListener
    {
        void PlaySfx(string flagId, CombatEventContext context, SfxPreset preset, string resolvedKey);
    }

    public interface ICombatEventReactionListener
    {
        void PlayReaction(string flagId, CombatEventContext context, CombatEventContext.CombatantRef target, int targetIndex);
    }
}
