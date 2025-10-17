using System;

namespace BattleV2.Providers
{
    public interface IBattleInputProvider
    {
        void RequestAction(BattleActionContext context, Action<Actions.ActionData> onSelected, Action onCancel);
    }
}
