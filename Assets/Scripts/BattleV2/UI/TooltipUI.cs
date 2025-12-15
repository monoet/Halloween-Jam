using TMPro;
using UnityEngine;

namespace BattleV2.UI
{
    /// <summary>
    /// Minimal tooltip controller: toggles a TMP text with the provided content.
    /// </summary>
    public sealed class TooltipUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text text;

        private void Awake()
        {
            if (text == null)
            {
                text = GetComponentInChildren<TMP_Text>();
            }
        }

        public void Show(string content)
        {
            if (text != null)
            {
                text.text = content ?? string.Empty;
            }

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
