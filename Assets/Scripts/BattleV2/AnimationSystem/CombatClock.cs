using System;
using UnityEngine;

namespace BattleV2.AnimationSystem
{
    public interface ICombatClock
    {
        double Now { get; }
        void Reset();
        void Sample();
    }

    /// <summary>
    /// Monotonic clock backed by Unity's unscaled time. Consumers
    /// must call <see cref="Sample"/> to update the cached value.
    /// </summary>
    public sealed class CombatClock : ICombatClock
    {
        private readonly Func<double> timeProvider;
        private double startTime;
        private double current;

        public CombatClock()
            : this(() => Time.unscaledTimeAsDouble)
        {
        }

        public CombatClock(Func<double> customProvider)
        {
            timeProvider = customProvider ?? throw new ArgumentNullException(nameof(customProvider));
            Reset();
        }

        public double Now => current;

        public void Reset()
        {
            startTime = timeProvider();
            current = 0d;
        }

        public void Sample()
        {
            double elapsed = timeProvider() - startTime;
            if (elapsed < 0d)
            {
                elapsed = 0d;
            }

            current = Math.Max(current, elapsed);
        }
    }
}
