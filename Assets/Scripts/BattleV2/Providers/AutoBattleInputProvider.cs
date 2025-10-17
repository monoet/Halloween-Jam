using System.Linq;
using BattleV2.Actions;
using BattleV2.Core;
using UnityEngine;

namespace BattleV2.Providers
{
    [CreateAssetMenu(menuName = "Battle/Input Provider/Auto")]
    public class AutoBattleInputProvider : ScriptableObject, IBattleInputProvider
    {
        public void RequestAction(BattleActionContext context, System.Action<ActionData> onSelected, System.Action onCancel)
        {
            if (context.AvailableActions == null || context.AvailableActions.Count == 0)
            {
                BattleLogger.Warn("AutoProvider", "No available actions – cancelling.");
                onCancel?.Invoke();
                return;
            }

            var chosen = context.AvailableActions.First();
            BattleLogger.Log("AutoProvider", $"Auto-selecting {chosen.id}");
            onSelected?.Invoke(chosen);
        }
    }
}
