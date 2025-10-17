using System;
using BattleV2.Providers;

namespace BattleV2.Charge
{
    /// <summary>
    /// Placeholder strategy that will combine CP charge with timed-hit sequences.
    /// </summary>
    public sealed class TimedHitChargeStrategy : IChargeStrategy
    {
        private ChargeRequest request;
        private Action<BattleSelection> onCompleted;
        private Action onCancelled;

        public void Begin(ChargeRequest request, Action<BattleSelection> onCompleted, Action onCancelled)
        {
            this.request = request;
            this.onCompleted = onCompleted;
            this.onCancelled = onCancelled;

            // Placeholder: immediately forward the selection without additional logic.
            onCompleted?.Invoke(new BattleSelection(request.Action, 0, request.Profile));
        }

        public void Tick(float deltaTime)
        {
            // Timed-hit behaviour pending implementation.
        }

        public void Cancel()
        {
            onCancelled?.Invoke();
        }
    }
}
