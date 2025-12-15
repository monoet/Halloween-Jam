namespace BattleV2.AnimationSystem.Execution.Runtime
{
    public interface ITimedHitInputProvider
    {
        /// <summary>
        /// Attempts to consume input from the provider.
        /// Returns true if input was detected, and outputs the timestamp.
        /// </summary>
        bool TryConsumeInput(out double timestamp);
    }
}
