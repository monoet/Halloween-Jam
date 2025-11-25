using System;
using UnityEngine;
using UnityEngine.UI;

namespace BattleV2.UI
{
    /// <summary>
    /// Submenú de ataques físicos.
    /// </summary>
    public sealed class AtkMenuPanel : MonoBehaviour
    {
        [Serializable]
        private struct AttackEntry
        {
            public string actionId;
            public Button button;
        }

        [SerializeField] private AttackEntry[] attacks = Array.Empty<AttackEntry>();
        [SerializeField] private Button backButton;

        public event Action<string> OnAttackChosen;
        public event Action OnBack;

        private void Awake()
        {
            if (attacks != null)
            {
                for (int i = 0; i < attacks.Length; i++)
                {
                    var entry = attacks[i];
                    if (entry.button == null || string.IsNullOrWhiteSpace(entry.actionId))
                    {
                        continue;
                    }

                    var id = entry.actionId;
                    entry.button.onClick.AddListener(() => OnAttackChosen?.Invoke(id));
                }
            }

            backButton?.onClick.AddListener(() => OnBack?.Invoke());
        }
    }
}
