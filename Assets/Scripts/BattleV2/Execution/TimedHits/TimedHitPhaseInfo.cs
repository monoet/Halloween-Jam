namespace BattleV2.Execution.TimedHits
{
    public readonly struct TimedHitPhaseInfo
    {
        public TimedHitPhaseInfo(int index, int totalPhases, float windowStartNormalized, float windowEndNormalized)
        {
            Index = index;
            TotalPhases = totalPhases;
            WindowStartNormalized = windowStartNormalized;
            WindowEndNormalized = windowEndNormalized;
        }

        public int Index { get; }
        public int TotalPhases { get; }
        public float WindowStartNormalized { get; }
        public float WindowEndNormalized { get; }
    }
}

