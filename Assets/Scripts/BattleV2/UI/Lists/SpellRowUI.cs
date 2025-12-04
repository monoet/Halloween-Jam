using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BattleV2.UI.Lists
{
    public interface IActionRowUIBehaviour
    {
        void ClearHandlers();
        bool IsEnabledForSubmit { get; }
        GameObject FocusTarget { get; }
        GameObject gameObject { get; }
    }

    public sealed class SpellRowUI : MonoBehaviour, ISelectHandler, ISubmitHandler, IActionRowUIBehaviour
    {
        [SerializeField] private Selectable selectable;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text spCostText;
        [SerializeField] private Image elementIconImage;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField, Range(0.1f, 1f)] private float disabledAlpha = 0.5f;
        [SerializeField] private bool showSpCostInRow = false;

        private ISpellRowData data;
        private Action<ISpellRowData> onHover;
        private Action<ISpellRowData> onSubmit;
        private Action<ISpellRowData> onBlocked;

        public bool IsEnabledForSubmit => data != null && data.IsEnabled;
        public GameObject FocusTarget => selectable != null ? selectable.gameObject : gameObject;

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

            if (spCostText != null)
            {
                if (showSpCostInRow)
                {
                    spCostText.text = data != null ? $"SP {Mathf.Max(0, data.SpCost)}" : string.Empty;
                    spCostText.gameObject.SetActive(true);
                }
                else
                {
                    spCostText.text = string.Empty;
                    spCostText.gameObject.SetActive(false);
                }
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

            if (data.IsEnabled)
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
