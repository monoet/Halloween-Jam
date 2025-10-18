using System;

namespace BattleV2.Charge
{
    public readonly struct TimedHitResult
    {
        public TimedHitResult(int hitsSucceeded, int totalHits, int cpRefund, float damageMultiplier)
        {
            HitsSucceeded = hitsSucceeded;
            TotalHits = totalHits;
            CpRefund = cpRefund;
            DamageMultiplier = damageMultiplier;
        }

        public int HitsSucceeded { get; }
        public int TotalHits { get; }
        public int CpRefund { get; }
        public float DamageMultiplier { get; }
    }
}

