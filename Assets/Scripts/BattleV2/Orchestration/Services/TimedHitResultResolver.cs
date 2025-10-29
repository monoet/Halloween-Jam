using BattleV2.Actions;
using BattleV2.Charge;
using BattleV2.Providers;
using UnityEngine;

namespace BattleV2.Orchestration.Services
{
    public interface ITimedHitResultResolver
    {
        TimedHitResult? Resolve(BattleSelection selection, IAction implementation, TimedHitResult? timedResult);
    }

    /// <summary>
    /// Normalizes timed hit outputs so callers always receive a sensible result, even when
    /// the pipeline skips user input (timeouts, cancelled sequences, etc.).
    /// </summary>
    public sealed class TimedHitResultResolver : ITimedHitResultResolver
    {
        public TimedHitResult? Resolve(BattleSelection selection, IAction implementation, TimedHitResult? timedResult)
        {
            if (implementation is LunarChainAction)
            {
                return ResolveLunarChain(selection, timedResult);
            }

            return timedResult;
        }

        private static TimedHitResult? ResolveLunarChain(BattleSelection selection, TimedHitResult? timedResult)
        {
            if (timedResult.HasValue)
            {
                var raw = timedResult.Value;
                int totalHits = Mathf.Max(1, raw.TotalHits);
                int hitsSucceeded = Mathf.Clamp(raw.HitsSucceeded, 0, totalHits);

                return new TimedHitResult(
                    hitsSucceeded,
                    totalHits,
                    raw.CpRefund,
                    raw.DamageMultiplier,
                    raw.Cancelled,
                    raw.SuccessStreak,
                    raw.PhaseDamageApplied,
                    raw.TotalDamageApplied);
            }

            int fallbackHits = Mathf.Max(1, selection.TimedHitProfile != null
                ? selection.TimedHitProfile.GetTierForCharge(selection.CpCharge).Hits
                : 1);

            return new TimedHitResult(
                fallbackHits,
                fallbackHits,
                cpRefund: 0,
                damageMultiplier: 1f,
                cancelled: false,
                successStreak: 0,
                phaseDamageApplied: false,
                totalDamageApplied: 0);
        }
    }
}
