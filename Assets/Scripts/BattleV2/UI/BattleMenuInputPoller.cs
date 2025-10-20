using UnityEngine;

namespace BattleV2.UI
{
    /// <summary>
    /// Polls raw input (keyboard/gamepad) to drive HUD ? Root menu transitions.
    /// </summary>
    public class BattleMenuInputPoller : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BattleMenuManager menuManager;

        [Header("Keys")]
        [SerializeField] private KeyCode confirmKey = KeyCode.V;
        [SerializeField] private KeyCode backKey = KeyCode.C;
        [SerializeField] private KeyCode upKey = KeyCode.W;
        [SerializeField] private KeyCode downKey = KeyCode.S;
        [SerializeField] private KeyCode leftKey = KeyCode.A;
        [SerializeField] private KeyCode rightKey = KeyCode.D;

        private void Update()
        {
            if (menuManager == null)
            {
                return;
            }

            bool confirm = Input.GetKeyDown(confirmKey) || Input.GetButtonDown("Submit");
            bool cancel = Input.GetKeyDown(backKey) || Input.GetButtonDown("Cancel");

            if (menuManager.IsHUDActive)
            {
                if (confirm)
                {
                    Debug.Log("[BattleMenuInput] Confirm from HUD -> RootMenu");
                    menuManager.OpenRootMenu();
                    return;
                }
            }
            else
            {
                if (cancel)
                {
                    Debug.Log("[BattleMenuInput] Cancel -> CloseCurrent");
                    menuManager.CloseCurrent();
                    return;
                }
            }

            // TODO: route WASD / stick navigation to the currently focused menu.
        }
    }
}

