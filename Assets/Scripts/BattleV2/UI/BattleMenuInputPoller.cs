using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BattleV2.UI
{
    /// <summary>
    /// Polls raw input (keyboard/gamepad) to drive HUD ↔ menus and route navigation to the focused UI.
    /// </summary>
    public class BattleMenuInputPoller : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BattleMenuManager menuManager;

        [Header("Keys")]
        [SerializeField] private KeyCode confirmKey = KeyCode.V;
        [SerializeField] private KeyCode backKey = KeyCode.C;

        private void Update()
        {
            if (menuManager == null)
            {
                return;
            }

            bool confirmPressed = Input.GetKeyDown(confirmKey) || Input.GetButtonDown("Submit");
            bool cancelPressed = Input.GetKeyDown(backKey) || Input.GetButtonDown("Cancel");

            if (menuManager.IsHUDActive)
            {
                if (confirmPressed)
                {
                    Debug.Log("[BattleMenuInput] Confirm from HUD -> RootMenu");
                    menuManager.OpenRootMenu();
                }
                return;
            }

            if (cancelPressed)
            {
                Debug.Log("[BattleMenuInput] Cancel -> CloseCurrent");
                menuManager.CloseCurrent();
                return;
            }

            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return;
            }

            EnsureSelection(eventSystem);

            if (confirmPressed)
            {
                TrySubmit(eventSystem);
            }

            // Navigation (WASD/arrow keys) is handled by Unity's StandaloneInputModule.
            // We just ensure a selection exists so axis input can drive it.
        }

        private void TrySubmit(EventSystem eventSystem)
        {
            var current = eventSystem.currentSelectedGameObject;
            if (current == null)
            {
                return;
            }

            var submitHandler = ExecuteEvents.submitHandler;
            if (submitHandler == null)
            {
                return;
            }

            var data = new BaseEventData(eventSystem);
            ExecuteEvents.Execute(current, data, submitHandler);
        }

        private void EnsureSelection(EventSystem eventSystem)
        {
            var current = eventSystem.currentSelectedGameObject;
            if (current != null && current.activeInHierarchy)
            {
                var selectable = current.GetComponent<Selectable>();
                if (selectable != null && selectable.interactable)
                {
                    return;
                }
            }

            var menu = menuManager.CurrentMenu;
            if (menu == null)
            {
                return;
            }

            var firstSelectable = menu.GetComponentInChildren<Selectable>();
            if (firstSelectable == null)
            {
                return;
            }

            eventSystem.SetSelectedGameObject(firstSelectable.gameObject);
            firstSelectable.Select();
        }
    }
}
