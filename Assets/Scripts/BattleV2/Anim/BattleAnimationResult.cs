namespace BattleV2.Anim
{
    /// <summary>
    /// Outcome produced by animation strategies to communicate flow-control requests back to the orchestrator.
    /// </summary>
    public readonly struct BattleAnimationResult
    {
        public static readonly BattleAnimationResult None = new BattleAnimationResult(false, 0f);

        public BattleAnimationResult(bool requestLock, float lockDurationSeconds)
        {
            RequestLock = requestLock;
            LockDurationSeconds = lockDurationSeconds;
        }

        public bool RequestLock { get; }
        public float LockDurationSeconds { get; }

        public static BattleAnimationResult LockFor(float seconds)
        {
            return new BattleAnimationResult(true, seconds);
        }
    }
}
