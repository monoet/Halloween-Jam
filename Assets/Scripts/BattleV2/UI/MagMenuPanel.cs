using System;
using UnityEngine;
using UnityEngine.UI;

namespace BattleV2.UI
{
    /// <summary>
    /// Submenú de hechizos.
    /// </summary>
    public sealed class MagMenuPanel : MonoBehaviour
    {
        [Serializable]
        private struct SpellEntry
        {
            public string spellId;
            public Button button;
        }

        [SerializeField] private SpellEntry[] spells = Array.Empty<SpellEntry>();
        [SerializeField] private Button backButton;

        public event Action<string> OnSpellChosen;
        public event Action OnBack;

        private void Awake()
        {
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
    }
}
