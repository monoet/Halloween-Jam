using System;
using BattleV2.Charge;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BattleV2.UI
{
    /// <summary>
    /// Handles the visual feedback and button wiring for CP charge adjustments.
    /// </summary>
    public class ChargeUIController : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text chargeLabel;
        [SerializeField] private Button increaseButton;
        [SerializeField] private Button decreaseButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private string chargeFormat = "CP Charge: {0}/{1}";

        private NotchedChargeStrategy boundStrategy;
        private Action onCancelRequested;

        private void OnDisable()
        {
            Unbind();
            SetPanelActive(false);
        }

        public void Show(NotchedChargeStrategy strategy, Action onConfirm, Action onCancel)
        {
            Unbind();
            boundStrategy = strategy;
            onCancelRequested = onCancel;

            if (increaseButton != null)
            {
                increaseButton.onClick.AddListener(HandleIncrease);
            }

            if (decreaseButton != null)
            {
                decreaseButton.onClick.AddListener(HandleDecrease);
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveAllListeners();
                confirmButton.onClick.AddListener(() => onConfirm?.Invoke());
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(HandleCancel);
            }

            SetPanelActive(true);
            UpdateCharge(boundStrategy?.CurrentCharge ?? 0, boundStrategy?.MaxCharge ?? 0);
        }

        public void UpdateCharge(int current, int max)
        {
            if (chargeLabel != null)
            {
                chargeLabel.text = string.Format(chargeFormat, current, max);
            }
        }

        public void Hide()
        {
            Unbind();
            SetPanelActive(false);
        }

        private void HandleIncrease()
        {
            boundStrategy?.AdjustCharge(1);
        }

        private void HandleDecrease()
        {
            boundStrategy?.AdjustCharge(-1);
        }

        private void HandleCancel()
        {
            onCancelRequested?.Invoke();
        }

        private void SetPanelActive(bool active)
        {
            if (panel != null)
            {
                panel.SetActive(active);
            }
        }

        private void Unbind()
        {
            if (increaseButton != null)
            {
                increaseButton.onClick.RemoveListener(HandleIncrease);
            }

            if (decreaseButton != null)
            {
                decreaseButton.onClick.RemoveListener(HandleDecrease);
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveAllListeners();
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(HandleCancel);
            }

            boundStrategy = null;
            onCancelRequested = null;
        }
    }
}
