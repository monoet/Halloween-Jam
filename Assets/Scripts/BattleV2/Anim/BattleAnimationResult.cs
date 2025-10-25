namespace BattleV2.Anim
{
    /// <summary>
    /// Outcome produced by animation strategies to communicate flow-control requests back to the orchestrator.
    /// </summary>
    public readonly struct BattleAnimationResult
    {
        public static readonly BattleAnimationResult None = new BattleAnimationResult(false, 0f, BattleAnimationStage.None);

        public BattleAnimationResult(bool requestLock, float lockDurationSeconds, BattleAnimationStage stage)
        {
            RequestLock = requestLock;
            LockDurationSeconds = lockDurationSeconds;
            Stage = stage;
        }

        public bool RequestLock { get; }
        public float LockDurationSeconds { get; }
        public BattleAnimationStage Stage { get; }

        public static BattleAnimationResult LockFor(float seconds, BattleAnimationStage stage)
        {
            return new BattleAnimationResult(true, seconds, stage);
        }

        public static BattleAnimationResult Immediate(BattleAnimationStage stage)
        {
            return new BattleAnimationResult(false, 0f, stage);
        }
    }
}
