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
            int successStreak = 0)
        {
            HitsSucceeded = hitsSucceeded;
            TotalHits = totalHits;
            CpRefund = cpRefund;
            DamageMultiplier = damageMultiplier;
            Cancelled = cancelled;
            SuccessStreak = successStreak;
        }

        public int HitsSucceeded { get; }
        public int TotalHits { get; }
        public int CpRefund { get; }
        public float DamageMultiplier { get; }
        public bool Cancelled { get; }
        public int SuccessStreak { get; }
    }
}


