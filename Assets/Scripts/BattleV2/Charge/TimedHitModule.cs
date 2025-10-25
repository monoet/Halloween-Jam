using System;
using UnityEngine;

namespace BattleV2.Charge
{
    /// <summary>
    /// Handles the timed-hit sequence logic for KS1 style actions.
    /// </summary>
    public class TimedHitModule
    {
        public event Action<int, int> OnPhaseStarted;
        public event Action<int, int, bool> OnPhaseResolved;
        public event Action<TimedHitResult> OnSequenceCompleted;

        public static event Action<int, int> GlobalPhaseStarted;
        public static event Action<int, int, bool> GlobalPhaseResolved;
        public static event Action<TimedHitResult> GlobalSequenceCompleted;

        public void StartSequence(Ks1TimedHitProfile profile, int cpCharge)
        {
            if (profile == null)
            {
                Complete(new TimedHitResult(0, 0, 0, 1f, cancelled: false, successStreak: 0));
                return;
            }

            var tier = profile.GetTierForCharge(cpCharge);
            int totalHits = Mathf.Max(0, tier.Hits);
            int hitsSucceeded = 0;
            float damageMultiplier = tier.DamageMultiplier > 0f ? tier.DamageMultiplier : 1f;

            for (int i = 0; i < totalHits; i++)
            {
                OnPhaseStarted?.Invoke(i + 1, totalHits);
                GlobalPhaseStarted?.Invoke(i + 1, totalHits);
                bool success = SimulateHit();
                if (success)
                {
                    hitsSucceeded++;
                }
                OnPhaseResolved?.Invoke(i + 1, totalHits, success);
                GlobalPhaseResolved?.Invoke(i + 1, totalHits, success);
            }

            int refund = Mathf.Clamp(hitsSucceeded, 0, tier.RefundMax);
            Complete(new TimedHitResult(hitsSucceeded, totalHits, refund, damageMultiplier, cancelled: false, successStreak: hitsSucceeded));
        }

        private bool SimulateHit()
        {
            // Placeholder: 80% success chance.
            return UnityEngine.Random.value <= 0.8f;
        }

        private void Complete(TimedHitResult result)
        {
            OnSequenceCompleted?.Invoke(result);
            GlobalSequenceCompleted?.Invoke(result);
        }
    }
}

