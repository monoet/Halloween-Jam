using System;
using BattleV2.Actions;
using BattleV2.Charge;
using BattleV2.Core;
using UnityEngine;

namespace BattleV2.Providers
{
    /// <summary>
    /// Temporary manual provider that downgrades to automatic choice until the new UI is implemented.
    /// </summary>
    public class ManualBattleInputProvider : MonoBehaviour, IBattleInputProvider
    {
        [SerializeField] private ChargeProfile defaultChargeProfile;

        public void RequestAction(BattleActionContext context, Action<BattleSelection> onSelected, Action onCancel)
        {
            if (context == null || context.AvailableActions == null || context.AvailableActions.Count == 0)
            {
                BattleLogger.Warn("Provider", "Manual provider received no actions. Cancelling.");
                onCancel?.Invoke();
                return;
            }

            var action = context.AvailableActions[0];
            var profile = ResolveProfile(context, action);
            BattleLogger.Log("Provider", "Manual provider degrading to auto (UI pending).");
            onSelected?.Invoke(new BattleSelection(action, 0, profile));
        }

        private ChargeProfile ResolveProfile(BattleActionContext context, BattleActionData action)
        {
            var catalog = context?.Context?.Catalog;
            var impl = catalog != null ? catalog.Resolve(action) : null;
            return impl != null ? impl.ChargeProfile : (defaultChargeProfile != null ? defaultChargeProfile : ChargeProfile.CreateRuntimeDefault());
        }
    }
}
