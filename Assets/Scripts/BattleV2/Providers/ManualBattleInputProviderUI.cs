using System;
using System.Collections.Generic;
using BattleV2.Actions;
using BattleV2.Charge;
using BattleV2.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BattleV2.Providers
{
    /// <summary>
    /// UI-driven manual provider that supports CP charge adjustment through a notched strategy.
    /// </summary>
    public class ManualBattleInputProviderUI : MonoBehaviour, IBattleInputProvider
    {
        [Header("UI Elements")]
        [SerializeField] private Transform buttonRoot;
        [SerializeField] private GameObject buttonTemplate;
        [SerializeField] private GameObject actionPanel;
        [SerializeField] private Button cancelButton;

        [Header("Charge UI")]
        [SerializeField] private GameObject chargePanel;
        [SerializeField] private TMP_Text chargeLabel;
        [SerializeField] private Button increaseChargeButton;
        [SerializeField] private Button decreaseChargeButton;
        [SerializeField] private Button confirmChargeButton;

        [Header("Formatting")]
        [SerializeField] private string labelFormat = "{0}";
        [SerializeField] private string costFormat = " (SP {0}/CP {1})";
        [SerializeField] private string chargeFormat = "CP Charge: {0}/{1}";

        [Header("Input Shortcuts")]
        [SerializeField] private KeyCode cancelKey = KeyCode.Escape;
        [SerializeField] private KeyCode confirmKey = KeyCode.Return;
        [SerializeField] private KeyCode increaseChargeKey = KeyCode.R;
        [SerializeField] private KeyCode decreaseChargeKey = KeyCode.F;

        [Header("Defaults")]
        [SerializeField] private ChargeProfile defaultChargeProfile;

        private readonly List<Button> spawnedButtons = new();
        private BattleActionContext pendingContext;
        private Action<BattleSelection> pendingOnSelected;
        private Action pendingOnCancel;

        private bool awaitingChoice;
        private NotchedChargeStrategy activeStrategy;
        private int currentCharge;
        private int currentMaxCharge;

        public void RequestAction(BattleActionContext context, Action<BattleSelection> onSelected, Action onCancel)
        {
            if (context == null || context.AvailableActions == null || context.AvailableActions.Count == 0)
            {
                BattleLogger.Warn("ProviderUI", "No actions available. Cancelling.");
                onCancel?.Invoke();
                return;
            }

            ClearStrategy();
            ClearButtons();

            pendingContext = context;
            pendingOnSelected = onSelected;
            pendingOnCancel = onCancel;
            awaitingChoice = true;

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
                increaseChargeButton.onClick.AddListener(() => activeStrategy?.AdjustCharge(1));
            }

            if (decreaseChargeButton != null)
            {
                decreaseChargeButton.onClick.AddListener(() => activeStrategy?.AdjustCharge(-1));
            }

            if (confirmChargeButton != null)
            {
                confirmChargeButton.onClick.AddListener(() => activeStrategy?.Confirm());
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
            activeStrategy?.Tick(Time.deltaTime);

            if (awaitingChoice && pendingContext != null && Input.GetKeyDown(cancelKey))
            {
                CancelRequest();
            }

            if (!awaitingChoice || pendingContext == null)
            {
                return;
            }

            // Buttons handle selection; keyboard shortcut for numbers can be added if needed.
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
                var capturedAction = action;
                button.onClick.AddListener(() => HandleActionButtonClicked(capturedAction));
                spawnedButtons.Add(button);
            }
        }

        private void HandleActionButtonClicked(BattleActionData action)
        {
            if (!awaitingChoice)
            {
                return;
            }

            awaitingChoice = false;
            StartChargeSequence(action);
        }

        private void StartChargeSequence(BattleActionData action)
        {
            var request = BuildChargeRequest(action);
            var bindings = new NotchedChargeStrategy.KeyBindings
            {
                Increase = increaseChargeKey,
                Decrease = decreaseChargeKey,
                Confirm = confirmKey,
                Cancel = cancelKey
            };

            activeStrategy = new NotchedChargeStrategy(bindings, true, msg => BattleLogger.Log("ProviderUI", msg), OnChargeChanged);
            activeStrategy.Begin(request, HandleChargeCompleted, HandleChargeCancelled);

            if (activeStrategy == null)
            {
                return;
            }

            SetActionPanelActive(false);
            SetChargePanelActive(true);
            UpdateChargeLabel();
        }

        private ChargeRequest BuildChargeRequest(BattleActionData action)
        {
            int availableCp = pendingContext.Player != null ? pendingContext.Player.CurrentCP : 0;
            int baseCost = Mathf.Max(0, action.costCP);
            var profile = ResolveChargeProfile(action) ?? defaultChargeProfile;
            return new ChargeRequest(pendingContext, action, profile, availableCp, baseCost);
        }

        private ChargeProfile ResolveChargeProfile(BattleActionData action)
        {
            var catalog = pendingContext?.Context?.Catalog;
            var impl = catalog != null ? catalog.Resolve(action) : null;
            return impl != null ? impl.ChargeProfile : null;
        }

        private void HandleChargeCompleted(BattleSelection selection)
        {
            ClearStrategy();
            SetChargePanelActive(false);
            SetActionPanelActive(false);
            ClearButtons();

            var callback = pendingOnSelected;
            ClearPending();
            callback?.Invoke(selection);
        }

        private void HandleChargeCancelled()
        {
            ClearStrategy();
            awaitingChoice = true;
            SetChargePanelActive(false);
            SetActionPanelActive(true);
        }

        private void HandleCancelClicked()
        {
            if (activeStrategy != null)
            {
                activeStrategy.Cancel();
            }
            else
            {
                CancelRequest();
            }
        }

        private void CancelRequest()
        {
            var cancel = pendingOnCancel;
            ClearPending();
            cancel?.Invoke();
        }

        private void ClearPending()
        {
            ClearStrategy();
            ClearButtons();
            SetActionPanelActive(false);
            SetChargePanelActive(false);
            pendingContext = null;
            pendingOnSelected = null;
            pendingOnCancel = null;
            awaitingChoice = false;
            currentCharge = 0;
            currentMaxCharge = 0;
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

        private void ClearStrategy()
        {
            activeStrategy = null;
            currentCharge = 0;
            currentMaxCharge = 0;
            UpdateChargeLabel();
        }

        private void OnChargeChanged(int current, int max)
        {
            currentCharge = current;
            currentMaxCharge = max;
            UpdateChargeLabel();
        }

        private void UpdateChargeLabel()
        {
            if (chargeLabel != null)
            {
                chargeLabel.text = string.Format(chargeFormat, currentCharge, currentMaxCharge);
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
    }
}
