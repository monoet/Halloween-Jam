using System;
using BattleV2.Actions;
using BattleV2.Core;
using UnityEngine;

namespace BattleV2.Providers
{
    /// <summary>
    /// Runtime keyboard-driven provider intended for early manual testing.
    /// Allows CP charge adjustment via keyboard notches before confirming the action.
    /// </summary>
    public class ManualBattleInputProviderV2 : MonoBehaviour, IBattleInputProvider
    {
        [Header("Input")]
        [SerializeField] private KeyCode cancelKey = KeyCode.Escape;
        [SerializeField] private KeyCode confirmKey = KeyCode.Return;
        [SerializeField] private KeyCode increaseChargeKey = KeyCode.R;
        [SerializeField] private KeyCode decreaseChargeKey = KeyCode.F;

        private BattleActionContext pendingContext;
        private Action<BattleSelection> pendingOnSelected;
        private Action pendingOnCancel;
        private bool awaitingChoice;
        private bool awaitingCharge;
        private BattleActionData pendingAction;
        private int pendingCharge;
        private int maxCharge;

        public void RequestAction(BattleActionContext context, Action<BattleSelection> onSelected, Action onCancel)
        {
            if (awaitingChoice || awaitingCharge)
            {
                BattleLogger.Warn("ProviderV2", "Already awaiting manual choice; cancelling previous request.");
                CancelPending();
            }

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
            awaitingCharge = false;
            pendingAction = null;
            pendingCharge = 0;
            maxCharge = 0;

            PrintOptions();
        }

        private void Update()
        {
            if (awaitingCharge)
            {
                HandleChargeInput();
                return;
            }

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
                BattleLogger.Log("ProviderV2", "Manual selection cancelled via key.");
                CancelPending();
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
            awaitingChoice = false;

            int availableCp = pendingContext.Player != null ? pendingContext.Player.CurrentCP : 0;
            int baseCost = Mathf.Max(0, action.costCP);
            maxCharge = Mathf.Max(0, availableCp - baseCost);
            pendingCharge = 0;
            pendingAction = action;

            if (maxCharge <= 0)
            {
                BattleLogger.Log("ProviderV2", $"Manual selecting {action.id} (slot {index + 1}) with CP Charge 0.");
                SubmitSelection(action, 0);
            }
            else
            {
                awaitingCharge = true;
                BattleLogger.Log("ProviderV2", $"Selected {action.id}. Use {increaseChargeKey}/{decreaseChargeKey} to adjust CP Charge (0-{maxCharge}), {confirmKey} to confirm, {cancelKey} to cancel.");
                PrintChargeStatus();
            }
        }

        private void HandleChargeInput()
        {
            if (pendingContext == null || pendingAction == null)
            {
                awaitingCharge = false;
                awaitingChoice = true;
                PrintOptions();
                return;
            }

            if (Input.GetKeyDown(increaseChargeKey))
            {
                pendingCharge = Mathf.Min(maxCharge, pendingCharge + 1);
                PrintChargeStatus();
            }

            if (Input.GetKeyDown(decreaseChargeKey))
            {
                pendingCharge = Mathf.Max(0, pendingCharge - 1);
                PrintChargeStatus();
            }

            if (Input.GetKeyDown(confirmKey))
            {
                BattleLogger.Log("ProviderV2", $"Confirming {pendingAction.id} with CP Charge {pendingCharge}.");
                SubmitSelection(pendingAction, pendingCharge);
            }

            if (Input.GetKeyDown(cancelKey))
            {
                BattleLogger.Log("ProviderV2", "Charge selection cancelled. Returning to action list.");
                awaitingCharge = false;
                awaitingChoice = true;
                pendingAction = null;
                pendingCharge = 0;
                PrintOptions();
            }
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

        private void PrintChargeStatus()
        {
            BattleLogger.Log("ProviderV2", $"CP Charge: {pendingCharge}/{maxCharge}");
        }

        private void SubmitSelection(BattleActionData action, int cpCharge)
        {
            awaitingCharge = false;
            awaitingChoice = false;
            var callback = pendingOnSelected;
            ClearPending();
            callback?.Invoke(new BattleSelection(action, cpCharge));
        }

        private void CancelPending()
        {
            var cancel = pendingOnCancel;
            ClearPending();
            cancel?.Invoke();
        }

        private void ClearPending()
        {
            pendingContext = null;
            pendingOnSelected = null;
            pendingOnCancel = null;
            awaitingChoice = false;
            awaitingCharge = false;
            pendingAction = null;
            pendingCharge = 0;
            maxCharge = 0;
        }
    }
}
