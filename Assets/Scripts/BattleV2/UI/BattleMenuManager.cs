using System.Collections.Generic;
using UnityEngine;

namespace BattleV2.UI
{
    /// <summary>
    /// Centralised controller that keeps track of battle menus without relying on nested hierarchies.
    /// </summary>
    public class BattleMenuManager : MonoBehaviour
    {
        [SerializeField] private GameObject defaultMenu;
        [SerializeField] private GameObject rootMenu;
        [SerializeField] private List<GameObject> menus = new();
        [SerializeField] private MenuScaleFadeTransition menuTransition;

        public bool IsHUDActive
        {
            get
            {
                if (defaultMenu == null)
                {
                    return openStack.Count == 0;
                }

                return openStack.Count == 0 || openStack.Peek() == defaultMenu;
            }
        }

        public GameObject CurrentMenu
        {
            get
            {
                if (openStack.Count == 0)
                {
                    return defaultMenu;
                }

                var top = openStack.Peek();
                return top != null ? top : defaultMenu;
            }
        }

        private const string DebugTag = "[BattleMenu]";
        private readonly Stack<GameObject> openStack = new();

        private void Awake()
        {
            foreach (var menu in menus)
            {
                if (menu == null)
                {
                    continue;
                }

                bool shouldBeActive = menu == defaultMenu;
                ApplyMenuState(menu, shouldBeActive);
            }

            EnsureDefaultOnTop();
            LogStack("Awake");
        }

        public void Open(GameObject menu)
        {
            if (menu == null)
            {
                return;
            }

            if (openStack.Count > 0 && openStack.Peek() == menu)
            {
                return;
            }

            var currentTop = openStack.Count > 0 ? openStack.Peek() : null;
            if (currentTop != null && currentTop != defaultMenu)
            {
                CloseCurrentInternal(fallbackToDefault: false);
            }

            ApplyMenuState(menu, true);
            openStack.Push(menu);

            LogStack($"Open -> {menu.name}");
        }

        public void OpenRootMenu()
        {
            if (rootMenu == null)
            {
                Debug.LogWarning("[BattleMenuManager] Root menu not assigned.");
                return;
            }

            Open(rootMenu);
        }

        public void CloseCurrent()
        {
            CloseCurrentInternal(fallbackToDefault: true);
        }

        public void CloseAll()
        {
            bool closedAny = false;
            while (openStack.Count > 0)
            {
                var current = openStack.Pop();
                if (current != null && current != defaultMenu)
                {
                    ApplyMenuState(current, false);
                    closedAny = true;
                }
            }

            EnsureDefaultOnTop();
            LogStack(closedAny ? "CloseAll" : "CloseAll (default only)");
        }

        private void CloseCurrentInternal(bool fallbackToDefault)
        {
            if (openStack.Count == 0)
            {
                EnsureDefaultOnTop();
                LogStack("CloseCurrent (empty)");
                return;
            }

            var current = openStack.Peek();
            if (current == defaultMenu)
            {
                LogStack("CloseCurrent (default stays active)");
                return;
            }

            current = openStack.Pop();
            ApplyMenuState(current, false);

            if (openStack.Count == 0 || (fallbackToDefault && openStack.Peek() != defaultMenu))
            {
                EnsureDefaultOnTop();
            }

            if (openStack.Count > 0)
            {
                var next = openStack.Peek();
                ApplyMenuState(next, true);
            }

            LogStack($"Close -> {current?.name ?? "(null)"}");
        }

        private void EnsureDefaultOnTop()
        {
            if (defaultMenu == null)
            {
                return;
            }

            if (openStack.Count == 0 || openStack.Peek() != defaultMenu)
            {
                ApplyMenuState(defaultMenu, true);
                openStack.Push(defaultMenu);
            }
        }

        private void ApplyMenuState(GameObject menu, bool isActive)
        {
            if (menu == null)
            {
                return;
            }

            bool canAnimate = menuTransition != null && menu != defaultMenu;

            if (isActive)
            {
                menu.SetActive(true);
                if (canAnimate)
                {
                    menuTransition.PlayOpen(menu);
                }
                else if (menu.TryGetComponent(out CanvasGroup activeGroup))
                {
                    activeGroup.alpha = 1f;
                    activeGroup.interactable = true;
                    activeGroup.blocksRaycasts = true;
                }

                EnsureMenuSelection(menu);
                return;
            }
            else
            {
                if (canAnimate)
                {
                    menuTransition.PlayClose(menu, () =>
                    {
                        menu.SetActive(false);
                    });
                    return;
                }

                if (menu.TryGetComponent(out CanvasGroup inactiveGroup))
                {
                    inactiveGroup.interactable = false;
                    inactiveGroup.blocksRaycasts = false;
                    inactiveGroup.alpha = 0f;
                }

                if (menu != defaultMenu)
                {
                    menu.SetActive(false);
                }
            }
        }

        private void EnsureMenuSelection(GameObject menu)
        {
            if (menu == null)
            {
                return;
            }

            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem == null)
            {
                return;
            }

            var selectable = menu.GetComponentInChildren<UnityEngine.UI.Selectable>();
            if (selectable == null || !selectable.gameObject.activeInHierarchy || !selectable.interactable)
            {
                return;
            }

            eventSystem.SetSelectedGameObject(selectable.gameObject);
            selectable.Select();
        }

        private void LogStack(string action)
        {
            var current = openStack.Count > 0 ? openStack.Peek() : null;
            string currentName = current != null ? current.name : "(none)";
            Debug.Log($"{DebugTag} {action}. Current: {currentName}");
        }
    }
}
