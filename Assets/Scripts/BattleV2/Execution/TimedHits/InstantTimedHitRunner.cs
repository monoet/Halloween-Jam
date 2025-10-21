using System;
using System.Threading.Tasks;
using BattleV2.Charge;

namespace BattleV2.Execution.TimedHits
{
    /// <summary>
    /// Default runner that resolves the timed hit immediately with full success.
    /// </summary>
    public sealed class InstantTimedHitRunner : ITimedHitRunner
    {
        public static InstantTimedHitRunner Shared { get; } = new InstantTimedHitRunner();

        public event Action OnSequenceStarted;
        public event Action<TimedHitPhaseInfo> OnPhaseStarted;
        public event Action<TimedHitPhaseResult> OnPhaseResolved;
        public event Action<TimedHitResult> OnSequenceCompleted;

        public Task<TimedHitResult> RunAsync(TimedHitRequest request)
        {
            OnSequenceStarted?.Invoke();

            int totalHits = request.Profile != null ? Math.Max(0, request.Profile.GetTierForCharge(request.CpCharge).Hits) : 0;
            int success = totalHits;
            var result = new TimedHitResult(success, totalHits, success, 1f, cancelled: false, successStreak: success);

            OnSequenceCompleted?.Invoke(result);
            return Task.FromResult(result);
        }
    }
}

