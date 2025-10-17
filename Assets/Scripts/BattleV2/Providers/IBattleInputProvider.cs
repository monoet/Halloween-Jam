using System;
using BattleV2.Actions;

namespace BattleV2.Providers
{
    public interface IBattleInputProvider
    {
        void RequestAction(BattleActionContext context, Action<ActionData> onSelected, Action onCancel);
    }
}
