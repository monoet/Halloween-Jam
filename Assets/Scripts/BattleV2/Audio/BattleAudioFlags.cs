namespace BattleV2.Audio
{
    /// <summary>
    /// Centralized string ids for combat audio flags to avoid magic strings in code and assets.
    /// NOTE: format is locked to slash-case (e.g., "attack/runback") to match current FMOD content; change only with FMOD authoring updates.
    /// </summary>
    public static class BattleAudioFlags
    {
        public const string AttackWindup = "attack/windup";
        public const string AttackImpact = "attack/impact";
        public const string AttackRunback = "attack/runback";
        public const string MarkApply = "mark/apply";
        public const string MarkDetonate = "mark/detonate";
        public const string UiTurnChange = "ui/turn_change";
    }
}
