namespace BattleV2.Execution.TimedHits
{
    /// <summary>
    /// Encapsulates the numeric parameters required to translate timed-hit phase outcomes into concrete damage.
    /// </summary>
    public readonly struct TimedHitPhaseDamagePlan
    {
        public TimedHitPhaseDamagePlan(
            float baseDamagePerHit,
            int minimumDamage,
            float tierDamageMultiplier,
            int totalPhases,
            bool allowPartialOnMiss = false)
        {
            BaseDamagePerHit = baseDamagePerHit;
            MinimumDamage = minimumDamage;
            TierDamageMultiplier = tierDamageMultiplier;
            TotalPhases = totalPhases;
            AllowPartialOnMiss = allowPartialOnMiss;
        }

        /// <summary>
        /// Base damage per hit before applying tier/phase multipliers.
        /// </summary>
        public float BaseDamagePerHit { get; }

        /// <summary>
        /// Minimum clamped damage per hit (after multipliers).
        /// </summary>
        public int MinimumDamage { get; }

        /// <summary>
        /// Baseline tier multiplier applied to every successful phase (normally >= 1).
        /// </summary>
        public float TierDamageMultiplier { get; }

        /// <summary>
        /// Total phases expected for the sequence (used for feedback and validation).
        /// </summary>
        public int TotalPhases { get; }

        /// <summary>
        /// True to allow per-phase damage even when <see cref="TimedHitPhaseResult.IsSuccess"/> is false (e.g., chip damage).
        /// </summary>
        public bool AllowPartialOnMiss { get; }
    }
}
