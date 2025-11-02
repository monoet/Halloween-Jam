using System;
using System.Collections.Generic;
using BattleV2.AnimationSystem.Runtime;

namespace BattleV2.AnimationSystem.Execution.Runtime
{
    public readonly struct TimedHitTolerance
    {
        public TimedHitTolerance(double perfectMilliseconds, double goodMilliseconds, double earlyMilliseconds, double lateMilliseconds)
        {
            PerfectMilliseconds = Math.Max(0d, perfectMilliseconds);
            GoodMilliseconds = Math.Max(PerfectMilliseconds, Math.Max(0d, goodMilliseconds));
            EarlyMilliseconds = Math.Max(0d, earlyMilliseconds);
            LateMilliseconds = Math.Max(0d, lateMilliseconds);
        }

        public double PerfectMilliseconds { get; }
        public double GoodMilliseconds { get; }
        public double EarlyMilliseconds { get; }
        public double LateMilliseconds { get; }

        public static TimedHitTolerance Default => new(45d, 120d, 90d, 90d);
    }

    public interface ITimedHitToleranceProfile
    {
        TimedHitTolerance Resolve(string tag);
    }

    public sealed class DefaultTimedHitToleranceProfile : ITimedHitToleranceProfile
    {
        private readonly TimedHitTolerance defaultTolerance;
        private readonly Dictionary<string, TimedHitTolerance> overrides;
        private readonly StringComparer comparer;

        public DefaultTimedHitToleranceProfile(
            TimedHitTolerance? defaultTolerance = null,
            IDictionary<string, TimedHitTolerance> overrides = null,
            StringComparer comparer = null)
        {
            this.defaultTolerance = defaultTolerance ?? TimedHitTolerance.Default;
            this.comparer = comparer ?? StringComparer.OrdinalIgnoreCase;

            if (overrides != null && overrides.Count > 0)
            {
                this.overrides = new Dictionary<string, TimedHitTolerance>(overrides, this.comparer);
            }
            else
            {
                this.overrides = new Dictionary<string, TimedHitTolerance>(this.comparer);
            }
        }

        public TimedHitTolerance Resolve(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return defaultTolerance;
            }

            return overrides.TryGetValue(tag, out var tolerance)
                ? tolerance
                : defaultTolerance;
        }

        public static DefaultTimedHitToleranceProfile FromAsset(TimedHitToleranceProfileAsset asset)
        {
            if (asset == null)
            {
                return new DefaultTimedHitToleranceProfile();
            }

            var overrides = asset.BuildOverridesDictionary();
            var defaultTolerance = asset.GetDefaultTolerance();
            return new DefaultTimedHitToleranceProfile(defaultTolerance, overrides);
        }
    }
}
