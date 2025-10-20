using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using BattleV2.UI;

namespace BattleV2.UI.ActionMenu
{
    /// <summary>
    /// Controls the root action menu (Attack, Skill, Guard, Item) and forwards navigation to submenus.
    /// </summary>
    public class ActionMenuController : MonoBehaviour
    {
        [Serializable]
        private struct RootMenuOption
        {
            public string id;
            public Button button;
            public GameObject targetMenu;
        }

        private const string DebugTag = "[ActionMenu]";

        [Header("References")]
        [SerializeField] private BattleMenuManager menuManager;
        [SerializeField] private RootMenuOption[] options;
        [SerializeField] private Button backButton;

        public event Action<string> OnOptionSelected;
        public event Action OnBackRequested;

        private readonly List<(Button button, UnityAction action)> registeredActions = new();

        private void Awake()
        {
            if (options != null)
            {
                foreach (var entry in options)
                {
                    if (entry.button == null)
                    {
                        continue;
                    }

                    var captured = entry;
                    UnityAction listener = () => HandleOption(captured);
                    entry.button.onClick.AddListener(listener);
                    registeredActions.Add((entry.button, listener));
                    EnsureFocusTween(entry.button.gameObject);
                }
            }

            if (backButton != null)
            {
                UnityAction backListener = HandleBack;
                backButton.onClick.AddListener(backListener);
                registeredActions.Add((backButton, backListener));
                EnsureFocusTween(backButton.gameObject);
            }
        }

        private void OnEnable()
        {
            TrySetInitialSelection();
        }

        private void OnDestroy()
        {
            foreach (var (button, action) in registeredActions)
            {
                button?.onClick.RemoveListener(action);
            }
            registeredActions.Clear();
        }

        private void HandleOption(RootMenuOption option)
        {
            Debug.Log($"{DebugTag} Selected {option.id}");
            OnOptionSelected?.Invoke(option.id);

            if (option.targetMenu != null && menuManager != null)
            {
                menuManager.Open(option.targetMenu);
            }
        }

        public void HandleBack()
        {
            Debug.Log($"{DebugTag} Back to HUD");
            OnBackRequested?.Invoke();
            menuManager?.CloseCurrent();
        }

        private void EnsureFocusTween(GameObject buttonObject)
        {
            if (buttonObject == null)
            {
                return;
            }

            if (!buttonObject.TryGetComponent<ButtonFocusTween>(out _))
            {
                buttonObject.AddComponent<ButtonFocusTween>();
            }
        }

        private void TrySetInitialSelection()
        {
            if (options == null || options.Length == 0)
            {
                return;
            }

            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return;
            }

            foreach (var entry in options)
            {
                var button = entry.button;
                if (button == null || !button.gameObject.activeInHierarchy || !button.interactable)
                {
                    continue;
                }

                eventSystem.SetSelectedGameObject(button.gameObject);
                button.Select();
                return;
            }

            if (backButton != null && backButton.gameObject.activeInHierarchy && backButton.interactable)
            {
                eventSystem.SetSelectedGameObject(backButton.gameObject);
                backButton.Select();
            }
        }
    }
}
