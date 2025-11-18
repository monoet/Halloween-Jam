using BattleV2.AnimationSystem;
using System;

namespace BattleV2.Charge
{
    public readonly struct TimedHitResult
    {
        public TimedHitResult(
            TimedHitJudgment judgment,
            int hitsSucceeded,
            int totalHits,
            float damageMultiplier,
            int phaseIndex,
            int totalPhases,
            bool isFinal,
            int cpRefund = 0,
            bool cancelled = false,
            int successStreak = 0,
            bool phaseDamageApplied = false,
            int totalDamageApplied = 0)
        {
            Judgment = judgment;
            HitsSucceeded = hitsSucceeded;
            TotalHits = totalHits;
            DamageMultiplier = damageMultiplier;
            PhaseIndex = phaseIndex;
            TotalPhases = totalPhases <= 0 ? 1 : totalPhases;
            IsFinal = isFinal;
            CpRefund = cpRefund;
            Cancelled = cancelled;
            SuccessStreak = successStreak;
            PhaseDamageApplied = phaseDamageApplied;
            TotalDamageApplied = totalDamageApplied;
        }

        public TimedHitResult(
            int hitsSucceeded,
            int totalHits,
            int cpRefund,
            float damageMultiplier,
            bool cancelled = false,
            int successStreak = 0,
            bool phaseDamageApplied = false,
            int totalDamageApplied = 0)
            : this(
                TimedHitJudgment.Miss,
                hitsSucceeded,
                totalHits,
                damageMultiplier,
                phaseIndex: 0,
                totalPhases: totalHits <= 0 ? 1 : totalHits,
                isFinal: true,
                cpRefund,
                cancelled,
                successStreak,
                phaseDamageApplied,
                totalDamageApplied)
        {
        }

        public TimedHitJudgment Judgment { get; }
        public int HitsSucceeded { get; }
        public int TotalHits { get; }
        public int CpRefund { get; }
        public float DamageMultiplier { get; }
        public bool Cancelled { get; }
        public int SuccessStreak { get; }
        public bool PhaseDamageApplied { get; }
        public int TotalDamageApplied { get; }
        public int PhaseIndex { get; }
        public int TotalPhases { get; }
        public bool IsFinal { get; }

        public TimedHitResult WithPhaseDamage(bool applied, int totalDamage)
        {
            return new TimedHitResult(
                Judgment,
                HitsSucceeded,
                TotalHits,
                DamageMultiplier,
                PhaseIndex,
                TotalPhases,
                IsFinal,
                CpRefund,
                Cancelled,
                SuccessStreak,
                applied,
                totalDamage);
        }
    }
}


