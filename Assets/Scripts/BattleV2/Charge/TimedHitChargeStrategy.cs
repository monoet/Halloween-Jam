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

            holdStrategy.Begin(request, HandleChargeCompleted, HandleChargeCancelled);
        }

        public void Tick(float deltaTime)
        {
            holdStrategy.Tick(deltaTime);
        }

        public void Cancel()
        {
            holdStrategy.Cancel();
        }

        private void HandleChargeCompleted(BattleSelection selection)
        {
            var profile = request.Profile as Ks1TimedHitProfile;
            if (profile != null)
            {
                timedHitModule.StartSequence(profile, selection.CpCharge);
            }

            Complete(selection);
        }

        private void HandleChargeCancelled()
        {
            onCancelled?.Invoke();
        }

        private void Complete(BattleSelection selection)
        {
            onCompleted?.Invoke(selection);
        }
    }
}
