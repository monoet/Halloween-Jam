using System;
using BattleV2.Actions;
using BattleV2.Charge;
using BattleV2.Core;
using UnityEngine;

namespace BattleV2.Providers
{
    /// <summary>
    /// Keyboard-driven charge provider that relies on a notched (incremental) strategy.
    /// </summary>
    public class ManualBattleInputProviderV2 : MonoBehaviour, IBattleInputProvider
    {
        [Header("Action Selection")]
        [SerializeField] private KeyCode cancelKey = KeyCode.Escape;
        [SerializeField] private ChargeProfile defaultChargeProfile;

        [Header("Charge Controls")]
        [SerializeField] private KeyCode confirmKey = KeyCode.Return;
        [SerializeField] private KeyCode increaseChargeKey = KeyCode.R;
        [SerializeField] private KeyCode decreaseChargeKey = KeyCode.F;

        private BattleActionContext pendingContext;
        private Action<BattleSelection> pendingOnSelected;
        private Action pendingOnCancel;

        private bool awaitingChoice;
        private IChargeStrategy activeStrategy;

        public void RequestAction(BattleActionContext context, Action<BattleSelection> onSelected, Action onCancel)
        {
            if (context == null || context.AvailableActions == null || context.AvailableActions.Count == 0)
            {
                BattleLogger.Warn("ProviderV2", "Manual provider received no actions. Cancelling.");
                onCancel?.Invoke();
                return;
            }

            pendingContext = context;
            pendingOnSelected = onSelected;
            pendingOnCancel = onCancel;
            awaitingChoice = true;
            ClearStrategy();

            PrintOptions();
        }

        private void Update()
        {
            activeStrategy?.Tick(Time.deltaTime);

            if (!awaitingChoice || pendingContext == null)
            {
                return;
            }

            for (int i = 0; i < Mathf.Min(9, pendingContext.AvailableActions.Count); i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                {
                    SelectAction(i);
                    return;
                }
            }

            if (Input.GetKeyDown(cancelKey))
            {
                CancelRequest();
            }
        }

        private void SelectAction(int index)
        {
            if (!awaitingChoice || pendingContext == null)
            {
                return;
            }

            if (index < 0 || index >= pendingContext.AvailableActions.Count)
            {
                BattleLogger.Warn("ProviderV2", $"Invalid action index {index} requested.");
                return;
            }

            var action = pendingContext.AvailableActions[index];
            BattleLogger.Log("ProviderV2", $"Selected {action.id} (slot {index + 1}).");
            StartChargeSequence(action);
        }

        private void StartChargeSequence(BattleActionData action)
        {
            awaitingChoice = false;

            var request = BuildChargeRequest(action);
            var bindings = new NotchedChargeStrategy.KeyBindings
            {
                Increase = increaseChargeKey,
                Decrease = decreaseChargeKey,
                Confirm = confirmKey,
                Cancel = cancelKey
            };

            var strategy = new NotchedChargeStrategy(bindings, true, msg => BattleLogger.Log("ProviderV2", msg));
            activeStrategy = strategy;
            strategy.Begin(request, HandleChargeCompleted, HandleChargeCancelled);
        }

        private ChargeRequest BuildChargeRequest(BattleActionData action)
        {
            int availableCp = pendingContext.Player != null ? pendingContext.Player.CurrentCP : 0;
            int baseCost = Mathf.Max(0, action.costCP);

            ResolveProfiles(action, out var chargeProfile, out var timedProfile);

            return new ChargeRequest(
                pendingContext,
                action,
                chargeProfile,
                availableCp,
                baseCost,
                timedProfile);
        }

        private void ResolveProfiles(BattleActionData action, out ChargeProfile chargeProfile, out Ks1TimedHitProfile timedProfile)
        {
            chargeProfile = defaultChargeProfile;
            timedProfile = null;

            var catalog = pendingContext?.Context?.Catalog;
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

        private void HandleChargeCompleted(BattleSelection selection)
        {
            ClearStrategy();
            awaitingChoice = false;
            var callback = pendingOnSelected;
            ClearPending();
            callback?.Invoke(selection);
        }

        private void HandleChargeCancelled()
        {
            ClearStrategy();
            awaitingChoice = true;
            BattleLogger.Log("ProviderV2", "Charge cancelled. Choose another action.");
            PrintOptions();
        }

        private void CancelRequest()
        {
            if (activeStrategy != null)
            {
                activeStrategy.Cancel();
                return;
            }

            var cancel = pendingOnCancel;
            ClearPending();
            cancel?.Invoke();
        }

        private void ClearStrategy()
        {
            activeStrategy = null;
        }

        private void ClearPending()
        {
            pendingContext = null;
            pendingOnSelected = null;
            pendingOnCancel = null;
            awaitingChoice = false;
            ClearStrategy();
        }

        private void PrintOptions()
        {
            if (pendingContext == null)
            {
                return;
            }

            BattleLogger.Log("ProviderV2", "Awaiting manual selection:");

            for (int i = 0; i < pendingContext.AvailableActions.Count; i++)
            {
                var action = pendingContext.AvailableActions[i];
                BattleLogger.Log("ProviderV2", $"{i + 1}. {action.id} (Cost SP:{action.costSP} CP:{action.costCP})");
            }

            BattleLogger.Log("ProviderV2", $"Press 1-{Mathf.Min(9, pendingContext.AvailableActions.Count)} to select, {cancelKey} to cancel.");
        }
    }
}
