using System;

namespace BattleV2.Charge
{
    public readonly struct TimedHitResult
    {
        public TimedHitResult(
            int hitsSucceeded,
            int totalHits,
            int cpRefund,
            float damageMultiplier,
            bool cancelled = false,
            int successStreak = 0,
            bool phaseDamageApplied = false,
            int totalDamageApplied = 0)
        {
            HitsSucceeded = hitsSucceeded;
            TotalHits = totalHits;
            CpRefund = cpRefund;
            DamageMultiplier = damageMultiplier;
            Cancelled = cancelled;
            SuccessStreak = successStreak;
            PhaseDamageApplied = phaseDamageApplied;
            TotalDamageApplied = totalDamageApplied;
        }

        public int HitsSucceeded { get; }
        public int TotalHits { get; }
        public int CpRefund { get; }
        public float DamageMultiplier { get; }
        public bool Cancelled { get; }
        public int SuccessStreak { get; }
        public bool PhaseDamageApplied { get; }
        public int TotalDamageApplied { get; }

        public TimedHitResult WithPhaseDamage(bool applied, int totalDamage)
        {
            return new TimedHitResult(
                HitsSucceeded,
                TotalHits,
                CpRefund,
                DamageMultiplier,
                Cancelled,
                SuccessStreak,
                applied,
                totalDamage);
        }
    }
}


