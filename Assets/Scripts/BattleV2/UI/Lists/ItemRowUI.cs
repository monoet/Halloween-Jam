using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BattleV2.UI.Lists
{
    public sealed class ItemRowUI : MonoBehaviour, ISelectHandler, ISubmitHandler, IActionRowUIBehaviour
    {
        [SerializeField] private Selectable selectable;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text quantityText;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField, Range(0.1f, 1f)] private float disabledAlpha = 0.5f;

        private IItemRowData data;
        private Action<IItemRowData> onHover;
        private Action<IItemRowData> onSubmit;
        private Action<IItemRowData> onBlocked;

        public bool IsEnabledForSubmit => data != null && data.IsEnabled && data.Quantity > 0;
        public GameObject FocusTarget => selectable != null ? selectable.gameObject : gameObject;

        public void Bind(
            IItemRowData rowData,
            Action<IItemRowData> hover,
            Action<IItemRowData> submit,
            Action<IItemRowData> blocked)
        {
            data = rowData;
            onHover = hover;
            onSubmit = submit;
            onBlocked = blocked;
            Render();
        }

        public void ClearHandlers()
        {
            onHover = null;
            onSubmit = null;
            onBlocked = null;
            data = null;
        }

        private void Render()
        {
            if (nameText != null)
            {
                nameText.text = data?.Name ?? string.Empty;
            }

            if (quantityText != null)
            {
                int qty = data != null ? Mathf.Max(0, data.Quantity) : 0;
                quantityText.text = qty > 0 ? $"x{qty}" : "x0";
            }

            UpdateVisualState();
        }

        private void UpdateVisualState()
        {
            if (canvasGroup != null)
            {
                bool enabled = data != null && data.IsEnabled && data.Quantity > 0;
                canvasGroup.alpha = enabled ? 1f : disabledAlpha;
            }
        }

        public void OnSelect(BaseEventData eventData)
        {
            HandleHover();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            HandleSubmit();
        }

        private void HandleHover()
        {
            if (data == null)
            {
                return;
            }

            onHover?.Invoke(data);
        }

        private void HandleSubmit()
        {
            if (data == null)
            {
                return;
            }

            if (data.IsEnabled && data.Quantity > 0)
            {
                onSubmit?.Invoke(data);
            }
            else
            {
                onBlocked?.Invoke(data);
            }
        }
    }
}
