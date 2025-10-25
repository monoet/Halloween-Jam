using System;

namespace BattleV2.Providers
{
    public interface IBattleInputProvider
    {
        void RequestAction(BattleActionContext context, Action<BattleSelection> onSelected, Action onCancel);
    }
}
