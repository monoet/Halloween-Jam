using System;
using System.Collections.Generic;
using UnityEngine;
using HalloweenJam.Combat;
using TMPro;
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

        private readonly List<Button> spawnedButtons = new();
        private Action<ActionData> onSelection;
        private Func<bool> interactionGuard;

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

            ClearButtons();
            EnsureActive(true);

            foreach (var action in actions)
            {
                if (action == null)
                {
                    continue;
                }

                var button = CreateButton();
                spawnedButtons.Add(button);

                var label = button.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.text = action.ActionName;
                }
                else
                {
                    var uiLabel = button.GetComponentInChildren<Text>();
                    if (uiLabel != null)
                    {
                        uiLabel.text = action.ActionName;
                    }
                }

                button.onClick.AddListener(() => HandleSelection(action));
            }
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
        }

        private void HandleSelection(ActionData action)
        {
            if (interactionGuard != null && interactionGuard())
            {
                Debug.Log("[ActionSelectionUI] Selection ignored because interaction is locked.");
                return;
            }

            var callback = onSelection;
            onSelection = null;

            Hide();

            callback?.Invoke(action);
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
