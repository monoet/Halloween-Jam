using TMPro;
using UnityEngine;

namespace BattleV2.UI.ActionMenu
{
    public class KeepsakeCardSlot : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text description;

        public CanvasGroup Group => canvasGroup;

        private void Awake()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }
        }

        public void SetContent(string displayName, string desc)
        {
            if (title != null)
            {
                title.text = displayName;
            }

            if (description != null)
            {
                description.text = desc;
            }
        }

        public void SetActive(bool active)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = active ? 1f : 0f;
                canvasGroup.blocksRaycasts = active;
                canvasGroup.interactable = active;
            }

            gameObject.SetActive(active);
        }
    }
}
