using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace BattleV2.UI
{
    /// <summary>
    /// Submenú de ítems.
    /// </summary>
    public sealed class ItemMenuPanel : BattlePanelBase, ICancelHandler
    {
        [Serializable]
        private struct ItemEntry
        {
            public string itemId;
            public Button button;
        }

        [SerializeField] private ItemEntry[] items = Array.Empty<ItemEntry>();
        [SerializeField] private Button backButton;
        [SerializeField] private Button defaultButton;

        public event Action<string> OnItemChosen;
        public event Action OnBack;

        protected override void Awake()
        {
            base.Awake();
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

        public override void FocusFirst()
        {
            Button target = defaultButton;
            if (target == null && items != null && items.Length > 0)
            {
                target = items[0].button;
            }

            if (target != null)
            {
                EventSystem.current?.SetSelectedGameObject(target.gameObject);
            }
        }

        public void OnCancel(BaseEventData eventData)
        {
            UIAudio.PlayBack();
            OnBack?.Invoke();
        }
    }
}
