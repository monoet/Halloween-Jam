using System;
using System.Threading.Tasks;
using BattleV2.Charge;

namespace BattleV2.Execution.TimedHits
{
    public interface ITimedHitRunner
    {
        event Action OnSequenceStarted;
        event Action<TimedHitPhaseInfo> OnPhaseStarted;
        event Action<TimedHitPhaseResult> OnPhaseResolved;
        event Action<TimedHitResult> OnSequenceCompleted;

        Task<TimedHitResult> RunAsync(TimedHitRequest request);
    }
}

