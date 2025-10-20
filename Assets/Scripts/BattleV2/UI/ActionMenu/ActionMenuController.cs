using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

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
                }
            }

            if (backButton != null)
            {
                UnityAction backListener = HandleBack;
                backButton.onClick.AddListener(backListener);
                registeredActions.Add((backButton, backListener));
            }
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
    }
}
