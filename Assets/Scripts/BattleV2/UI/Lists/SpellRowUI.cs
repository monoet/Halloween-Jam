using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Events;
using System;

namespace BattleV2.UI.Lists
{
    public interface IActionRowUIBehaviour
    {
        void ClearHandlers();
        bool IsEnabledForSubmit { get; }
        GameObject FocusTarget { get; }
        GameObject gameObject { get; }
    }

    public sealed class SpellRowUI : MonoBehaviour, ISelectHandler, ISubmitHandler, IDeselectHandler, IActionRowUIBehaviour
    {
        [SerializeField] private Selectable selectable;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Image elementIconImage;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField, Range(0.1f, 1f)] private float disabledAlpha = 0.5f;
        [SerializeField] private UISelectionFeedback selectionFeedback;
        [Header("Highlight Color (optional)")]
        [SerializeField] private bool useHighlightColor = false;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color highlightedColor = Color.yellow;

        private ISpellRowData data;
        private Action<ISpellRowData> onHover;
        private Action<ISpellRowData> onSubmit;
        private Action<ISpellRowData> onBlocked;
        private Button cachedButton;
        private UnityAction cachedClickHandler;
        private SelectionForwarder forwarder;

        public bool IsEnabledForSubmit => data != null && data.IsEnabled;
        public GameObject FocusTarget => Selectable != null ? Selectable.gameObject : gameObject;
        public int RowIndex { get; private set; }
        public Selectable Selectable => selectable;

        public void Bind(
            ISpellRowData rowData,
            Action<ISpellRowData> hover,
            Action<ISpellRowData> submit,
            Action<ISpellRowData> blocked)
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

            if (elementIconImage != null)
            {
                elementIconImage.sprite = data?.ElementIcon;
                elementIconImage.enabled = elementIconImage.sprite != null;
            }

            UpdateVisualState();
        }

        private void UpdateVisualState()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = (data != null && data.IsEnabled) ? 1f : disabledAlpha;
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

            if (data.IsEnabled)
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

            // If the selectable lives on a different GO, forward select/deselect so feedback always triggers.
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
