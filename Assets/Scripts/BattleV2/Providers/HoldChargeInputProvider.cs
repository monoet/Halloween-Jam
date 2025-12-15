using System;
using BattleV2.Actions;
using BattleV2.Charge;
using BattleV2.Core;
using BattleV2.Execution.TimedHits;
using UnityEngine;

namespace BattleV2.Providers
{
    /// <summary>
    /// Manual provider that uses the hold-to-charge strategy (keyboard prototype).
    /// </summary>
    public class HoldChargeInputProvider : MonoBehaviour, IBattleInputProvider
    {
        [SerializeField] private KeyCode holdKey = KeyCode.R;
        [SerializeField] private KeyCode cancelKey = KeyCode.Escape;
        [SerializeField] private ChargeProfile defaultChargeProfile;

        private BattleActionContext pendingContext;
        private Action<BattleSelection> pendingOnSelected;
        private Action pendingOnCancel;
        private HoldChargeStrategy strategy;
        private bool awaitingResult;

        public void RequestAction(BattleActionContext context, Action<BattleSelection> onSelected, Action onCancel)
        {
            if (context == null || context.AvailableActions == null || context.AvailableActions.Count == 0)
            {
                BattleLogger.Warn("HoldProvider", "No actions available. Cancelling.");
                onCancel?.Invoke();
                return;
            }

            pendingContext = context;
            pendingOnSelected = onSelected;
            pendingOnCancel = onCancel;
            awaitingResult = true;
            strategy = new HoldChargeStrategy(holdKey);

            var action = context.AvailableActions[0];
            BattleLogger.Log("HoldProvider", $"Auto-selecting {action.id}. Hold {holdKey} to charge.");

            var request = BuildChargeRequest(action);
            strategy.Begin(request, HandleCompleted, HandleCancelled);
        }

        private void Update()
        {
            if (!awaitingResult)
            {
                return;
            }

            strategy?.Tick(Time.deltaTime);

            if (Input.GetKeyDown(cancelKey))
            {
                awaitingResult = false;
                strategy?.Cancel();
                pendingOnCancel?.Invoke();
                ClearPending();
            }
        }

        private ChargeRequest BuildChargeRequest(BattleActionData action)
        {
            int availableCp = pendingContext.Player != null ? pendingContext.Player.CurrentCP : 0;
            int baseCost = Mathf.Max(0, action.costCP);

            ResolveProfiles(action, out var chargeProfile, out var timedProfile, out var basicProfile, out var runnerKind);

            return new ChargeRequest(
                pendingContext,
                action,
                chargeProfile,
                availableCp,
                baseCost,
                timedProfile,
                basicProfile,
                runnerKind);
        }

        private void ResolveProfiles(
            BattleActionData action,
            out ChargeProfile chargeProfile,
            out Ks1TimedHitProfile timedProfile,
            out BasicTimedHitProfile basicProfile,
            out TimedHitRunnerKind runnerKind)
        {
            chargeProfile = defaultChargeProfile;
            timedProfile = action != null ? action.timedHitProfile : null;
            basicProfile = action != null ? action.basicTimedHitProfile : null;
            runnerKind = action != null ? action.runnerKind : TimedHitRunnerKind.Default;

            var catalog = pendingContext?.Context?.Catalog;
            var impl = catalog != null ? catalog.Resolve(action) : null;

            if (impl != null)
            {
                if (impl.ChargeProfile != null)
                {
                    chargeProfile = impl.ChargeProfile;
                }

                if (timedProfile == null && impl is ITimedHitAction timedHitAction)
                {
                    timedProfile = timedHitAction.TimedHitProfile;
                }

                if (basicProfile == null && impl is IBasicTimedHitAction basicTimedAction && basicTimedAction.BasicTimedHitProfile != null)
                {
                    basicProfile = basicTimedAction.BasicTimedHitProfile;
                    runnerKind = TimedHitRunnerKind.Basic;
                }
            }

            if (basicProfile != null)
            {
                runnerKind = TimedHitRunnerKind.Basic;
            }

            if (chargeProfile == null)
            {
                chargeProfile = defaultChargeProfile != null
                    ? defaultChargeProfile
                    : ChargeProfile.CreateRuntimeDefault();
            }
        }

        private void HandleCompleted(BattleSelection selection)
        {
            awaitingResult = false;
            var callback = pendingOnSelected;
            ClearPending();
            callback?.Invoke(selection);
        }

        private void HandleCancelled()
        {
            awaitingResult = false;
            pendingOnCancel?.Invoke();
            ClearPending();
        }

        private void ClearPending()
        {
            pendingContext = null;
            pendingOnSelected = null;
            pendingOnCancel = null;
            strategy = null;
        }
    }
}
