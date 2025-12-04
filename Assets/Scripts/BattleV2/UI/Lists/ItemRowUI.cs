using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Events;
using System;

namespace BattleV2.UI.Lists
{
    public sealed class ItemRowUI : MonoBehaviour, ISelectHandler, ISubmitHandler, IDeselectHandler, IActionRowUIBehaviour
    {
        [SerializeField] private Selectable selectable;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text quantityText;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField, Range(0.1f, 1f)] private float disabledAlpha = 0.5f;
        [SerializeField] private UISelectionFeedback selectionFeedback;
        [Header("Highlight Color (optional)")]
        [SerializeField] private bool useHighlightColor = false;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color highlightedColor = Color.yellow;

        private IItemRowData data;
        private Action<IItemRowData> onHover;
        private Action<IItemRowData> onSubmit;
        private Action<IItemRowData> onBlocked;
        private Button cachedButton;
        private UnityAction cachedClickHandler;
        private SelectionForwarder forwarder;

        public bool IsEnabledForSubmit => data != null && data.IsEnabled && data.Quantity > 0;
        public GameObject FocusTarget => Selectable != null ? Selectable.gameObject : gameObject;
        public int RowIndex { get; private set; }
        public Selectable Selectable => selectable;

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
            selectionFeedback ??= selectable != null
                ? selectable.GetComponent<UISelectionFeedback>() ?? selectable.gameObject.AddComponent<UISelectionFeedback>()
                : GetComponent<UISelectionFeedback>() ?? gameObject.AddComponent<UISelectionFeedback>();
            cachedButton = selectable as Button;
            EnsureButtonBinding();
            EnsureSelectionForwarder();
            Render();
            ApplyColor(false);
        }

        public void ClearHandlers()
        {
            ResetVisual();
            onHover = null;
            onSubmit = null;
            onBlocked = null;
            data = null;
            if (cachedButton != null && cachedClickHandler != null)
            {
                cachedButton.onClick.RemoveListener(cachedClickHandler);
            }
            cachedButton = null;
            forwarder = null;
        }

        public void SetIndex(int index)
        {
            RowIndex = index;
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
            selectionFeedback?.OnSelect(eventData);
            ApplyColor(true);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            HandleSubmit();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            selectionFeedback?.OnDeselect(eventData);
            ApplyColor(false);
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

        private void EnsureButtonBinding()
        {
            if (cachedButton == null)
            {
                return;
            }

            cachedClickHandler ??= OnButtonClicked;
            cachedButton.onClick.RemoveListener(cachedClickHandler);
            cachedButton.onClick.AddListener(cachedClickHandler);
        }

        private void OnButtonClicked()
        {
            HandleSubmit();
        }

        private void ResetVisual()
        {
            if (selectionFeedback != null)
            {
                selectionFeedback.ResetInstant();
            }

            ApplyColor(false);
        }

        private void EnsureSelectionForwarder()
        {
            if (selectable == null)
            {
                return;
            }

            forwarder = selectable.gameObject.GetComponent<SelectionForwarder>();
            if (forwarder == null)
            {
                forwarder = selectable.gameObject.AddComponent<SelectionForwarder>();
            }

            forwarder.Configure(OnSelectForward, OnDeselectForward);
        }

        private void OnSelectForward(BaseEventData evt)
        {
            OnSelect(evt);
        }

        private void OnDeselectForward(BaseEventData evt)
        {
            OnDeselect(evt);
        }

        private void ApplyColor(bool highlighted)
        {
            if (!useHighlightColor || nameText == null)
            {
                return;
            }

            nameText.color = highlighted ? highlightedColor : normalColor;
        }
    }
}
