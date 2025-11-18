using System;
using BattleV2.Actions;
using BattleV2.Charge;
using BattleV2.Core;
using BattleV2.Execution.TimedHits;
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
            ResolveProfiles(context, action, out var chargeProfile, out var timedProfile, out var basicProfile, out var runnerKind);
            BattleLogger.Log("Provider", "Manual provider degrading to auto (UI pending).");
            onSelected?.Invoke(new BattleSelection(action, 0, chargeProfile, timedProfile, basicTimedHitProfile: basicProfile, runnerKind: runnerKind));
        }

        private void ResolveProfiles(
            BattleActionContext context,
            BattleActionData action,
            out ChargeProfile chargeProfile,
            out Ks1TimedHitProfile timedProfile,
            out BasicTimedHitProfile basicProfile,
            out TimedHitRunnerKind runnerKind)
        {
            var catalog = context?.Context?.Catalog;
            var impl = catalog != null ? catalog.Resolve(action) : null;
            chargeProfile = defaultChargeProfile;
            timedProfile = null;
            basicProfile = null;
            runnerKind = TimedHitRunnerKind.Default;

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

                if (impl is IBasicTimedHitAction basicTimedAction && basicTimedAction.BasicTimedHitProfile != null)
                {
                    basicProfile = basicTimedAction.BasicTimedHitProfile;
                    runnerKind = TimedHitRunnerKind.Basic;
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
