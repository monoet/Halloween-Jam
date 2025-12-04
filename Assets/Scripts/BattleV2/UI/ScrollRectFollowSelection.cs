using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using BattleV2.UI.Lists;

namespace BattleV2.UI
{
    /// <summary>
    /// Keeps the selected element inside a ScrollRect viewport when navigating with keyboard/gamepad.
    /// Works best for vertical lists (Mag/Item).
    /// </summary>
    [RequireComponent(typeof(ScrollRect))]
    public sealed class ScrollRectFollowSelection : MonoBehaviour
    {
        [SerializeField] private float margin = 10f; // extra padding in pixels
        [SerializeField, Tooltip("Units per second to approach target scroll position. 0 = instant.")]
        private float scrollSpeed = 8f;
        [SerializeField, Tooltip("How many rows are fully visible in the viewport. Used for slot-based scrolling.")]
        private int visibleSlots = 4;

        private ScrollRect scrollRect;
        private RectTransform viewport;
        private RectTransform content;
        private float? pendingNormalized;
        private int lastIndex = -1;
        private int firstVisibleIndex = 0; // tracked in indices, not derived from normalized

        private void Awake()
        {
            scrollRect = GetComponent<ScrollRect>();
            viewport = scrollRect != null ? scrollRect.viewport : null;
            content = scrollRect != null ? scrollRect.content : null;
        }

        private void LateUpdate()
        {
            if (EventSystem.current == null || viewport == null || content == null)
            {
                return;
            }

            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null)
            {
                return;
            }

            // Climb to the row root (SpellRowUI / ItemRowUI) to get the real index.
            var spellRow = selected.GetComponentInParent<SpellRowUI>();
            var itemRow = spellRow == null ? selected.GetComponentInParent<ItemRowUI>() : null;

            RectTransform rowTransform = null;
            int rowIndex = -1;

            if (spellRow != null)
            {
                rowTransform = spellRow.transform as RectTransform;
                rowIndex = spellRow.RowIndex;
            }
            else if (itemRow != null)
            {
                rowTransform = itemRow.transform as RectTransform;
                rowIndex = itemRow.RowIndex;
            }
            else
            {
                return; // selection is not part of a known row
            }

            if (rowTransform == null || !rowTransform.IsChildOf(content))
            {
                return; // selection is not inside this ScrollRect content
            }

            EnsureVisible(rowIndex);

            if (pendingNormalized.HasValue && scrollRect != null)
            {
                float current = scrollRect.verticalNormalizedPosition;
                float desired = pendingNormalized.Value;

                if (scrollSpeed <= 0f)
                {
                    scrollRect.verticalNormalizedPosition = desired;
                    if (scrollRect.verticalScrollbar != null)
                    {
                        scrollRect.verticalScrollbar.SetValueWithoutNotify(desired);
                    }
                    pendingNormalized = null;
                }
                else
                {
                    float step = scrollSpeed * Time.unscaledDeltaTime;
                    float next = Mathf.MoveTowards(current, desired, step);
                    scrollRect.verticalNormalizedPosition = next;
                    if (scrollRect.verticalScrollbar != null)
                    {
                        scrollRect.verticalScrollbar.SetValueWithoutNotify(next);
                    }
                    if (Mathf.Approximately(next, desired))
                    {
                        pendingNormalized = null;
                    }
                }
            }
        }

        private void EnsureVisible(int index)
        {
            int activeChildren = 0;
            for (int i = 0; i < content.childCount; i++)
            {
                if (content.GetChild(i).gameObject.activeInHierarchy)
                {
                    activeChildren++;
                }
            }

            if (activeChildren <= visibleSlots || activeChildren == 0)
            {
                return; // nothing to scroll
            }

            int maxFirst = Mathf.Max(0, activeChildren - visibleSlots);
            int dir = (lastIndex < 0) ? 0 : Mathf.Clamp(index - lastIndex, -1, 1);
            lastIndex = index;

            // Row relative to current window
            int row = index - firstVisibleIndex;

            int upperKeep = (visibleSlots - 1) / 2; // e.g., 1 when visibleSlots=4
            int lowerKeep = visibleSlots / 2;       // e.g., 2 when visibleSlots=4

            int desiredFirst = firstVisibleIndex;

            if (dir < 0)
            {
                // Moving up: if we go above upperKeep, scroll up so we land on upperKeep
                if (row < upperKeep)
                {
                    desiredFirst = index - upperKeep;
                }
            }
            else if (dir > 0)
            {
                // Moving down: if we go below lowerKeep, scroll down so we land on lowerKeep
                if (row > lowerKeep)
                {
                    desiredFirst = index - lowerKeep;
                }
            }
            else
            {
                return; // no movement yet
            }

            desiredFirst = Mathf.Clamp(desiredFirst, 0, maxFirst);
            if (desiredFirst == firstVisibleIndex)
            {
                return;
            }

            firstVisibleIndex = desiredFirst;

            // Convert desiredFirst (top-most visible index) to normalized position
            float normalized = 1f - Mathf.InverseLerp(0f, maxFirst, desiredFirst);
            pendingNormalized = Mathf.Clamp01(normalized);
        }
    }
}
