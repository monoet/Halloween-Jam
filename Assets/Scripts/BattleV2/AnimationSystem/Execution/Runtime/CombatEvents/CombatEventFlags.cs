namespace BattleV2.AnimationSystem.Execution.Runtime.CombatEvents
{
    public static class CombatEventFlags
    {
        public const string Windup = "attack/windup";
        public const string Runup = "attack/runup";
        public const string Impact = "attack/impact";
        public const string Runback = "attack/runback";
        public const string ActionCancel = "action/cancel";

        // Timed-hit outcome flags (emitted from TimedHitAudioBridge)
        public const string Missed = "attack/missed";
        public const string Success = "attack/success";

        // Legacy alias kept for existing references (maps to Success).
        public const string TimedHitSuccess = Success;
    }
}
