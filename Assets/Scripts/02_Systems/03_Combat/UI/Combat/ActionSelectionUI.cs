using System;
using System.Collections.Generic;
using DG.Tweening;
using HalloweenJam.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HalloweenJam.UI.Combat
{
    /// <summary>
    /// Lightweight action picker that instantiates UI buttons at runtime.
    /// </summary>
    public sealed class ActionSelectionUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Transform buttonContainer;
        [SerializeField] private Button buttonPrefab;

        [Header("Behavior")]
        [SerializeField] private bool hideOnAwake = true;

        private enum SelectionState
        {
            Idle,
            Showing,
            AwaitingSelection,
            Locked
        }

        private readonly List<Button> spawnedButtons = new();
        private Action<ActionData> onSelection;
        private Func<bool> interactionGuard;
        private SelectionState state = SelectionState.Idle;

        public bool IsAwaitingSelection => state == SelectionState.AwaitingSelection;
        public bool CanShow => state == SelectionState.Idle;

        private void Awake()
        {
            if (panelRoot == null && buttonContainer != null)
            {
                panelRoot = buttonContainer.gameObject;
            }

            if (hideOnAwake)
            {
                Hide();
            }
        }

        public void Show(RuntimeCombatEntity entity, Action<ActionData> callback)
        {
            if (!CanShow)
            {
                Debug.LogWarning("[ActionSelectionUI] Show called while already showing. Resetting.");
                Hide();
            }

            if (entity == null)
            {
                Debug.LogWarning("[ActionSelectionUI] No combat entity provided.");
                return;
            }

            var actions = entity.AvailableActions;
            if (actions == null || actions.Count == 0)
            {
                Debug.LogWarning("[ActionSelectionUI] Entity has no available actions.");
                EnsureActive(false);
                return;
            }

            onSelection = callback;
            state = SelectionState.Showing;

            ClearButtons();
            EnsureActive(true);

            int availableCp = entity.CombatantState != null ? entity.CombatantState.CurrentCP : 0;
            int availableSp = entity.CombatantState != null ? entity.CombatantState.CurrentSP : int.MaxValue;

            foreach (var action in actions)
            {
                if (action == null)
                {
                    continue;
                }

                var button = CreateButton();
                spawnedButtons.Add(button);

                bool canAfford = HasResources(action, availableCp, availableSp);
                UpdateButtonVisual(button, action, canAfford);
                button.interactable = canAfford;

                var capturedAction = action;
                button.onClick.AddListener(() => HandleButtonClick(capturedAction, button, canAfford));
            }

            if (spawnedButtons.Count == 0)
            {
                Debug.LogWarning("[ActionSelectionUI] No valid actions to display.");
                Hide();
                return;
            }

            state = SelectionState.AwaitingSelection;
            Debug.LogFormat("[ActionSelectionUI] Opened with {0} actions.", spawnedButtons.Count);
        }

        public void SetInteractionGuard(Func<bool> guard)
        {
            interactionGuard = guard;
        }

        public void Hide()
        {
            ClearButtons();
            EnsureActive(false);
            onSelection = null;
            currentEntity = null;
            state = SelectionState.Idle;
        }

        private void HandleButtonClick(ActionData action, Button button, bool canAfford)
        {
            if (state != SelectionState.AwaitingSelection)
            {
                return;
            }

            if (!canAfford)
            {
                Debug.Log("[ActionSelectionUI] Action selected but not affordable.");
                return;
            }

            if (interactionGuard != null && interactionGuard())
            {
                Debug.Log("[ActionSelectionUI] Selection ignored because interaction is locked.");
                return;
            }

            state = SelectionState.Locked;

            foreach (var btn in spawnedButtons)
            {
                if (btn != null)
                {
                    btn.interactable = false;
                }
            }

            AnimateButtonSelection(button, () => CompleteSelection(action));
        }

        private void CompleteSelection(ActionData action)
        {
            var callback = onSelection;
            onSelection = null;
            Hide();
            callback?.Invoke(action);
        }

        private bool HasResources(ActionData action, int availableCp, int availableSp)
        {
            bool cpOk = action.CpCost <= availableCp;
            bool spOk = action.SpCost <= availableSp;
            return cpOk && spOk;
        }

        private void UpdateButtonVisual(Button button, ActionData action, bool canAfford)
        {
            var label = button.GetComponentInChildren<TMP_Text>();
            if (label == null)
            {
                var uiLabel = button.GetComponentInChildren<Text>();
                if (uiLabel != null)
                {
                    uiLabel.text = action.ActionName;
                }
                return;
            }

            string name = action.ActionName;
            string cpCost = action.CpCost > 0 ? $"CP {action.CpCost}" : string.Empty;
            string spCost = action.SpCost > 0 ? $"SP {action.SpCost}" : string.Empty;
            var parts = new List<string>(2);
            if (!string.IsNullOrEmpty(cpCost)) parts.Add(cpCost);
            if (!string.IsNullOrEmpty(spCost)) parts.Add(spCost);
            string costLine = string.Join("  ", parts);

            string color = canAfford ? "#FFFFFF" : "#888888";
            if (!string.IsNullOrEmpty(costLine))
            {
                label.text = $"{name}\n<size=18><color={color}>{costLine}</color></size>";
            }
            else
            {
                label.text = name;
            }
        }

        private void AnimateButtonSelection(Button button, Action onComplete)
        {
            if (button == null)
            {
                onComplete?.Invoke();
                return;
            }

            var target = button.transform;
            target.DOKill();
            target.DOScale(target.localScale * 1.05f, 0.1f)
                .SetLoops(2, LoopType.Yoyo)
                .SetEase(Ease.OutQuad)
                .OnComplete(() => onComplete?.Invoke());
        }

        private void EnsureActive(bool active)
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(active);
            }
            else if (buttonContainer != null)
            {
                buttonContainer.gameObject.SetActive(active);
            }
            else
            {
                gameObject.SetActive(active);
            }
        }

        private void ClearButtons()
        {
            foreach (var button in spawnedButtons)
            {
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    Destroy(button.gameObject);
                }
            }

            spawnedButtons.Clear();
        }

        private Button CreateButton()
        {
            if (buttonContainer == null)
            {
                Debug.LogWarning("[ActionSelectionUI] Button container missing; using self transform.");
                buttonContainer = transform;
            }

            if (buttonPrefab != null)
            {
                return Instantiate(buttonPrefab, buttonContainer);
            }

            var buttonGO = new GameObject("ActionButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var rect = buttonGO.GetComponent<RectTransform>();
            rect.SetParent(buttonContainer, false);
            rect.sizeDelta = new Vector2(200f, 48f);

            var labelGO = new GameObject("Label", typeof(RectTransform));
            var labelRect = labelGO.GetComponent<RectTransform>();
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TMP_Text tmpLabel = labelGO.AddComponent<TMP_Text>();
            tmpLabel.alignment = TextAlignmentOptions.Center;
            tmpLabel.fontSize = 24f;

            var image = buttonGO.GetComponent<Image>();
            image.color = new Color(0.2f, 0.25f, 0.3f, 0.9f);

            return buttonGO.GetComponent<Button>();
        }
    }
}


