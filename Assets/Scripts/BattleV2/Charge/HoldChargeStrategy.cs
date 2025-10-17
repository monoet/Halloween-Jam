using System;
using BattleV2.Providers;

namespace BattleV2.Charge
{
    /// <summary>
    /// Placeholder for the hold-to-charge mechanic. Implementation will handle press/hold timings.
    /// </summary>
    public sealed class HoldChargeStrategy : IChargeStrategy
    {
        private ChargeRequest request;
        private Action<BattleSelection> onCompleted;
        private Action onCancelled;

        public void Begin(ChargeRequest request, Action<BattleSelection> onCompleted, Action onCancelled)
        {
            this.request = request;
            this.onCompleted = onCompleted;
            this.onCancelled = onCancelled;

            // Placeholder behaviour: immediately complete with zero charge.
            onCompleted?.Invoke(new BattleSelection(request.Action, 0, request.Profile));
        }

        public void Tick(float deltaTime)
        {
            // Hold behaviour pending implementation.
        }

        public void Cancel()
        {
            onCancelled?.Invoke();
        }
    }
}
