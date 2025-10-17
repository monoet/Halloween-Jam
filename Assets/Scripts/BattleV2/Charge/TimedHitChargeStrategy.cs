using System;
using BattleV2.Providers;

namespace BattleV2.Charge
{
    /// <summary>
    /// Strategy that executes charge followed by a timed-hit sequence.
    /// </summary>
    public sealed class TimedHitChargeStrategy : IChargeStrategy
    {
        private readonly HoldChargeStrategy holdStrategy = new HoldChargeStrategy();
        private readonly TimedHitModule timedHitModule = new TimedHitModule();

        private ChargeRequest request;
        private Action<BattleSelection> onCompleted;
        private Action onCancelled;
        private bool waitingTimedHits;
        private BattleSelection cachedSelection;

        public event Action<int, int> OnPhaseStarted
        {
            add => timedHitModule.OnPhaseStarted += value;
            remove => timedHitModule.OnPhaseStarted -= value;
        }

        public event Action<int, int, bool> OnPhaseResolved
        {
            add => timedHitModule.OnPhaseResolved += value;
            remove => timedHitModule.OnPhaseResolved -= value;
        }

        public event Action<TimedHitResult> OnSequenceCompleted
        {
            add => timedHitModule.OnSequenceCompleted += value;
            remove => timedHitModule.OnSequenceCompleted -= value;
        }

        public void Begin(ChargeRequest request, Action<BattleSelection> onCompleted, Action onCancelled)
        {
            this.request = request;
            this.onCompleted = onCompleted;
            this.onCancelled = onCancelled;
            waitingTimedHits = false;
            cachedSelection = default;

            holdStrategy.Begin(request, HandleChargeCompleted, HandleChargeCancelled);
        }

        public void Tick(float deltaTime)
        {
            if (!waitingTimedHits)
            {
                holdStrategy.Tick(deltaTime);
                return;
            }

            timedHitModule.StartSequence(request.Profile as Ks1TimedHitProfile, cachedSelection.CpCharge);
            Complete(cachedSelection);
        }

        public void Cancel()
        {
            if (!waitingTimedHits)
            {
                holdStrategy.Cancel();
                return;
            }

            waitingTimedHits = false;
            onCancelled?.Invoke();
        }

        private void HandleChargeCompleted(BattleSelection selection)
        {
            waitingTimedHits = true;
            cachedSelection = selection;
        }

        private void HandleChargeCancelled()
        {
            onCancelled?.Invoke();
        }

        private void Complete(BattleSelection selection)
        {
            waitingTimedHits = false;
            onCompleted?.Invoke(selection);
        }
    }
}
