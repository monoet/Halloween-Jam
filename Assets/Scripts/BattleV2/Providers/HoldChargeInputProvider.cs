using System;
using BattleV2.Actions;
using BattleV2.Charge;
using BattleV2.Core;
using UnityEngine;

namespace BattleV2.Providers
{
    /// <summary>
    /// Manual provider that uses the hold-to-charge strategy (keyboard prototype).
    /// </summary>
    public class HoldChargeInputProvider : MonoBehaviour, IBattleInputProvider
    {
        [SerializeField] private KeyCode confirmKey = KeyCode.Return;
        [SerializeField] private KeyCode cancelKey = KeyCode.Escape;
        [SerializeField] private ChargeProfile defaultChargeProfile;

        private BattleActionContext pendingContext;
        private Action<BattleSelection> pendingOnSelected;
        private Action pendingOnCancel;
        private HoldChargeStrategy strategy = new HoldChargeStrategy();
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

            var action = context.AvailableActions[0];
            BattleLogger.Log("HoldProvider", $"Auto-selecting {action.id}. Hold key to charge.");

            var request = BuildChargeRequest(action);
            strategy.Begin(request, HandleCompleted, HandleCancelled);
        }

        private void Update()
        {
            if (!awaitingResult)
            {
                return;
            }

            strategy.Tick(Time.deltaTime);

            if (Input.GetKeyDown(confirmKey))
            {
                strategy.Cancel();
            }

            if (Input.GetKeyDown(cancelKey))
            {
                awaitingResult = false;
                pendingOnCancel?.Invoke();
                ClearPending();
            }
        }

        private ChargeRequest BuildChargeRequest(BattleActionData action)
        {
            int availableCp = pendingContext.Player != null ? pendingContext.Player.CurrentCP : 0;
            int baseCost = Mathf.Max(0, action.costCP);
            var profile = ResolveChargeProfile(action) ?? defaultChargeProfile ?? ChargeProfile.CreateRuntimeDefault();
            return new ChargeRequest(pendingContext, action, profile, availableCp, baseCost);
        }

        private ChargeProfile ResolveChargeProfile(BattleActionData action)
        {
            var catalog = pendingContext?.Context?.Catalog;
            var impl = catalog != null ? catalog.Resolve(action) : null;
            return impl != null ? impl.ChargeProfile : null;
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
        }
    }
}
