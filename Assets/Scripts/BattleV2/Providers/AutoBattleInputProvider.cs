using System.Linq;
using BattleV2.Actions;
using BattleV2.Charge;
using BattleV2.Core;
using UnityEngine;

namespace BattleV2.Providers
{
    [CreateAssetMenu(menuName = "Battle/Input Provider/Auto")]
    public class AutoBattleInputProvider : ScriptableObject, IBattleInputProvider
    {
        [SerializeField] private ChargeProfile defaultChargeProfile;

        public void RequestAction(BattleActionContext context, System.Action<BattleSelection> onSelected, System.Action onCancel)
        {
            if (context.AvailableActions == null || context.AvailableActions.Count == 0)
            {
                BattleLogger.Warn("AutoProvider", "No available actions; cancelling.");
                onCancel?.Invoke();
                return;
            }

            var chosen = context.AvailableActions.First();
            ResolveProfiles(context, chosen, out var chargeProfile, out var timedProfile);

            BattleLogger.Log("AutoProvider", $"Auto-selecting {chosen.id}");
            onSelected?.Invoke(new BattleSelection(chosen, 0, chargeProfile, timedProfile));
        }

        private void ResolveProfiles(
            BattleActionContext context,
            BattleActionData action,
            out ChargeProfile chargeProfile,
            out Ks1TimedHitProfile timedProfile)
        {
            chargeProfile = defaultChargeProfile;
            timedProfile = null;

            var catalog = context?.Context?.Catalog;
            var impl = catalog != null ? catalog.Resolve(action) : null;

            if (impl != null)
            {
                if (impl.ChargeProfile != null)
                {
                    chargeProfile = impl.ChargeProfile;
                }

                if (impl is ITimedHitAction timedHitAction)
                {
                    timedProfile = timedHitAction.TimedHitProfile;
                }
            }

            if (chargeProfile == null)
            {
                chargeProfile = defaultChargeProfile != null
                    ? defaultChargeProfile
                    : ChargeProfile.CreateRuntimeDefault();
            }
        }
    }
}
