using System;
using BattleV2.Actions;
using BattleV2.Core;
using UnityEngine;

namespace BattleV2.Providers
{
    /// <summary>
    /// Temporary manual provider that downgrades to automatic choice until the new UI is implemented.
    /// </summary>
    public class ManualBattleInputProvider : MonoBehaviour, IBattleInputProvider
    {
        public void RequestAction(BattleActionContext context, Action<BattleSelection> onSelected, Action onCancel)
        {
            if (context == null || context.AvailableActions == null || context.AvailableActions.Count == 0)
            {
                BattleLogger.Warn("Provider", "Manual provider received no actions. Cancelling.");
                onCancel?.Invoke();
                return;
            }

            BattleLogger.Log("Provider", "Manual provider degrading to auto (UI pending).");
            onSelected?.Invoke(new BattleSelection(context.AvailableActions[0]));
        }
    }
}
