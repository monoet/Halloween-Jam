using System;
using BattleV2.Providers;

namespace BattleV2.Charge
{
    public interface IChargeStrategy
    {
        void Begin(ChargeRequest request, Action<BattleSelection> onCompleted, Action onCancelled);
        void Tick(float deltaTime);
        void Cancel();
    }
}

