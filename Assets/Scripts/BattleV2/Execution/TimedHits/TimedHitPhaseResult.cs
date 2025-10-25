namespace BattleV2.Execution.TimedHits
{
    public readonly struct TimedHitPhaseResult
    {
        public TimedHitPhaseResult(int index, bool isSuccess, float damageMultiplier, float accuracyNormalized)
        {
            Index = index;
            IsSuccess = isSuccess;
            DamageMultiplier = damageMultiplier;
            AccuracyNormalized = accuracyNormalized;
        }

        public int Index { get; }
        public bool IsSuccess { get; }
        public float DamageMultiplier { get; }
        public float AccuracyNormalized { get; }
    }
}

