namespace BattleV2.AnimationSystem.Execution.Runtime.CombatEvents
{
    public static class CombatEventFlags
    {
        public const string Windup = "attack/windup";
        public const string Runup = "attack/runup";
        public const string Impact = "attack/impact";
        public const string Runback = "attack/runback";
        public const string ActionCancel = "action/cancel";

        // Obsoletos: resultados de timed-hit ahora se emiten solo via BattleAudioFlags.attack/timed/*
        [System.Obsolete("Use attack/timed/* flags via TimedHitAudioBridge")]
        public const string Missed = "attack/missed";

        [System.Obsolete("Use attack/timed/impact or attack/timed/perfect")]
        public const string Success = "attack/success";

        [System.Obsolete("Use attack/timed/impact or attack/timed/perfect")]
        public const string TimedHitSuccess = Success;
    }
}
