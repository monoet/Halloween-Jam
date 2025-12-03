using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace BattleV2.UI
{
    /// <summary>
    /// Submenú de hechizos.
    /// </summary>
    public sealed class MagMenuPanel : BattlePanelBase, ICancelHandler
    {
        [Serializable]
        private struct SpellEntry
        {
            public string spellId;
            public Button button;
        }

        [SerializeField] private SpellEntry[] spells = Array.Empty<SpellEntry>();
        [SerializeField] private Button backButton;
        [SerializeField] private Button defaultButton;

        public event Action<string> OnSpellChosen;
        public event Action OnBack;

        protected override void Awake()
        {
            base.Awake();
            if (spells != null)
            {
                for (int i = 0; i < spells.Length; i++)
                {
                    var entry = spells[i];
                    if (entry.button == null || string.IsNullOrWhiteSpace(entry.spellId))
                    {
                        continue;
                    }

                    var id = entry.spellId;
                    entry.button.onClick.AddListener(() => OnSpellChosen?.Invoke(id));
                }
            }

            backButton?.onClick.AddListener(() => OnBack?.Invoke());
        }

        public override void FocusFirst()
        {
            Button target = defaultButton;
            if (target == null && spells != null && spells.Length > 0)
            {
                target = spells[0].button;
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
