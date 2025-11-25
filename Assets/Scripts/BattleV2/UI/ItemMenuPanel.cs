using System;
using UnityEngine;
using UnityEngine.UI;

namespace BattleV2.UI
{
    /// <summary>
    /// Submenú de ítems.
    /// </summary>
    public sealed class ItemMenuPanel : MonoBehaviour
    {
        [Serializable]
        private struct ItemEntry
        {
            public string itemId;
            public Button button;
        }

        [SerializeField] private ItemEntry[] items = Array.Empty<ItemEntry>();
        [SerializeField] private Button backButton;

        public event Action<string> OnItemChosen;
        public event Action OnBack;

        private void Awake()
        {
            if (items != null)
            {
                for (int i = 0; i < items.Length; i++)
                {
                    var entry = items[i];
                    if (entry.button == null || string.IsNullOrWhiteSpace(entry.itemId))
                    {
                        continue;
                    }

                    var id = entry.itemId;
                    entry.button.onClick.AddListener(() => OnItemChosen?.Invoke(id));
                }
            }

            backButton?.onClick.AddListener(() => OnBack?.Invoke());
        }
    }
}
