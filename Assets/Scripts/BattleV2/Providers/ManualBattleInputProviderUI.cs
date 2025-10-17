using System;
using System.Collections.Generic;
using BattleV2.Actions;
using BattleV2.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BattleV2.Providers
{
    /// <summary>
    /// Manual input provider that renders the available actions into UI buttons and supports CP charge adjustment.
    /// </summary>
    public class ManualBattleInputProviderUI : MonoBehaviour, IBattleInputProvider
    {
        [Header("UI Elements")]
        [Tooltip("Parent container where action buttons will be spawned.")]
        [SerializeField] private Transform buttonRoot;
        [Tooltip("Prefab containing a Button (and label) that will be cloned per action.")]
        [SerializeField] private GameObject buttonTemplate;
        [Tooltip("Optional cancel button that triggers the fallback callback.")]
        [SerializeField] private Button cancelButton;
        [Tooltip("Optional panel GameObject to toggle visibility while awaiting input.")]
        [SerializeField] private GameObject actionPanel;

        [Header("Charge Controls")]
        [Tooltip("Panel shown while adjusting CP charge.")]
        [SerializeField] private GameObject chargePanel;
        [SerializeField] private TMP_Text chargeLabel;
        [SerializeField] private Button increaseChargeButton;
        [SerializeField] private Button decreaseChargeButton;
        [SerializeField] private Button confirmChargeButton;

        [Header("Formatting")]
        [SerializeField] private string labelFormat = "{0}";
        [SerializeField] private string costFormat = " (SP {0}/CP {1})";
        [SerializeField] private string chargeFormat = "CP Charge: {0}/{1}";

        [Header("Keyboard Shortcuts")]
        [SerializeField] private KeyCode cancelKey = KeyCode.Escape;
        [SerializeField] private KeyCode confirmKey = KeyCode.Return;
        [SerializeField] private KeyCode increaseChargeKey = KeyCode.R;
        [SerializeField] private KeyCode decreaseChargeKey = KeyCode.F;

        private readonly List<Button> spawnedButtons = new();
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
            if (awaitingCharge)
            {
                CancelChargeSelection();
            }

            ClearButtons();

            if (context == null || context.AvailableActions == null || context.AvailableActions.Count == 0)
            {
                BattleLogger.Warn("ProviderUI", "No actions available for manual UI. Cancelling.");
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

            BuildButtons(context.AvailableActions);
            SetActionPanelActive(true);
            SetChargePanelActive(false);
        }

        private void Awake()
        {
            if (buttonTemplate != null)
            {
                buttonTemplate.SetActive(false);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(HandleCancelClicked);
            }

            if (increaseChargeButton != null)
            {
                increaseChargeButton.onClick.AddListener(() => AdjustCharge(1));
            }

            if (decreaseChargeButton != null)
            {
                decreaseChargeButton.onClick.AddListener(() => AdjustCharge(-1));
            }

            if (confirmChargeButton != null)
            {
                confirmChargeButton.onClick.AddListener(ConfirmChargeSelection);
            }

            SetActionPanelActive(false);
            SetChargePanelActive(false);
        }

        private void OnDestroy()
        {
            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(HandleCancelClicked);
            }

            if (increaseChargeButton != null)
            {
                increaseChargeButton.onClick.RemoveAllListeners();
            }

            if (decreaseChargeButton != null)
            {
                decreaseChargeButton.onClick.RemoveAllListeners();
            }

            if (confirmChargeButton != null)
            {
                confirmChargeButton.onClick.RemoveAllListeners();
            }
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

            if (Input.GetKeyDown(cancelKey))
            {
                HandleCancelClicked();
            }
        }

        private void BuildButtons(IReadOnlyList<BattleActionData> actions)
        {
            if (buttonTemplate == null || buttonRoot == null)
            {
                BattleLogger.Error("ProviderUI", "Button template or root missing.");
                return;
            }

            for (int i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                if (action == null)
                {
                    continue;
                }

                var instance = Instantiate(buttonTemplate, buttonRoot);
                instance.name = $"ActionButton_{action.id}";
                instance.SetActive(true);

                var button = instance.GetComponent<Button>();
                if (button == null)
                {
                    BattleLogger.Warn("ProviderUI", $"Button template missing Button component ({instance.name}).");
                    Destroy(instance);
                    continue;
                }

                string displayName = !string.IsNullOrWhiteSpace(action.displayName) ? action.displayName : action.id;
                string label = string.Format(labelFormat, displayName);
                if ((action.costSP > 0 || action.costCP > 0) && !string.IsNullOrEmpty(costFormat))
                {
                    label += string.Format(costFormat, action.costSP, action.costCP);
                }

                SetButtonLabel(instance, label);
                int capturedIndex = i;
                button.onClick.AddListener(() => HandleButtonClicked(capturedIndex));
                spawnedButtons.Add(button);
            }
        }

        private void HandleButtonClicked(int index)
        {
            if (!awaitingChoice || pendingContext == null)
            {
                return;
            }

            if (index < 0 || index >= pendingContext.AvailableActions.Count)
            {
                BattleLogger.Warn("ProviderUI", $"Clicked index {index} out of range.");
                return;
            }

            var action = pendingContext.AvailableActions[index];
            int availableCp = pendingContext.Player != null ? pendingContext.Player.CurrentCP : 0;
            int baseCost = Mathf.Max(0, action.costCP);
            pendingCharge = 0;
            maxCharge = Mathf.Max(0, availableCp - baseCost);
            pendingAction = action;

            if (maxCharge <= 0 || chargePanel == null)
            {
                SubmitSelection(action, 0);
            }
            else
            {
                awaitingChoice = false;
                awaitingCharge = true;
                SetActionPanelActive(false);
                SetChargePanelActive(true);
                UpdateChargeLabel();
            }
        }

        private void HandleChargeInput()
        {
            if (Input.GetKeyDown(increaseChargeKey))
            {
                AdjustCharge(1);
            }

            if (Input.GetKeyDown(decreaseChargeKey))
            {
                AdjustCharge(-1);
            }

            if (Input.GetKeyDown(confirmKey))
            {
                ConfirmChargeSelection();
            }

            if (Input.GetKeyDown(cancelKey))
            {
                CancelChargeSelection();
            }
        }

        private void AdjustCharge(int delta)
        {
            if (!awaitingCharge)
            {
                return;
            }

            pendingCharge = Mathf.Clamp(pendingCharge + delta, 0, maxCharge);
            UpdateChargeLabel();
        }

        private void ConfirmChargeSelection()
        {
            if (!awaitingCharge || pendingAction == null)
            {
                return;
            }

            SubmitSelection(pendingAction, pendingCharge);
        }

        private void CancelChargeSelection()
        {
            if (!awaitingCharge)
            {
                CancelPending();
                return;
            }

            awaitingCharge = false;
            awaitingChoice = true;
            pendingAction = null;
            pendingCharge = 0;
            SetChargePanelActive(false);
            SetActionPanelActive(true);
        }

        private void SubmitSelection(BattleActionData action, int cpCharge)
        {
            awaitingCharge = false;
            awaitingChoice = false;
            SetChargePanelActive(false);
            SetActionPanelActive(false);
            ClearButtons();

            var callback = pendingOnSelected;
            ClearPending();
            callback?.Invoke(new BattleSelection(action, cpCharge));
        }

        private void HandleCancelClicked()
        {
            if (awaitingCharge)
            {
                CancelChargeSelection();
            }
            else
            {
                CancelPending();
            }
        }

        private void CancelPending()
        {
            ClearButtons();
            SetActionPanelActive(false);
            SetChargePanelActive(false);
            var cancel = pendingOnCancel;
            ClearPending();
            cancel?.Invoke();
        }

        private void ClearButtons()
        {
            for (int i = 0; i < spawnedButtons.Count; i++)
            {
                if (spawnedButtons[i] != null)
                {
                    spawnedButtons[i].onClick.RemoveAllListeners();
                    Destroy(spawnedButtons[i].gameObject);
                }
            }

            spawnedButtons.Clear();
        }

        private void SetButtonLabel(GameObject buttonObject, string text)
        {
            if (buttonObject == null)
            {
                return;
            }

            var tmp = buttonObject.GetComponentInChildren<TMP_Text>();
            if (tmp != null)
            {
                tmp.text = text;
                return;
            }

            var uiText = buttonObject.GetComponentInChildren<Text>();
            if (uiText != null)
            {
                uiText.text = text;
            }
        }

        private void UpdateChargeLabel()
        {
            if (chargeLabel != null)
            {
                chargeLabel.text = string.Format(chargeFormat, pendingCharge, maxCharge);
            }
        }

        private void SetActionPanelActive(bool active)
        {
            if (actionPanel != null)
            {
                actionPanel.SetActive(active);
            }
        }

        private void SetChargePanelActive(bool active)
        {
            if (chargePanel != null)
            {
                chargePanel.SetActive(active);
            }
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
