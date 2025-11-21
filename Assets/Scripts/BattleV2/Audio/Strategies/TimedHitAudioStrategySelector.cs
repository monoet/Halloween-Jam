namespace BattleV2.Audio.Strategies
{
    public static class TimedHitAudioStrategySelector
    {
        public static ITimedHitAudioStrategy Get(bool useHarness)
        {
            return useHarness
                ? new HarnessTimedHitAudioStrategy()
                : new RealTimedHitAudioStrategy();
        }
    }
}
