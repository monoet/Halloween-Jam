using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HalloweenJam.UI.Combat
{
    [RequireComponent(typeof(Button))]
    public class BattleActionMenuHover : MonoBehaviour, IPointerEnterHandler, ISelectHandler
    {
        private BattleActionMenu owner;
        private Button button;

        public void Initialize(BattleActionMenu menu, Button associatedButton)
        {
            owner = menu;
            button = associatedButton;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            NotifyOwner();
        }

        public void OnSelect(BaseEventData eventData)
        {
            NotifyOwner();
        }

        private void NotifyOwner()
        {
            if (owner == null || button == null)
            {
                return;
            }

            owner.NotifyPointerEnter(button);
        }
    }
}
