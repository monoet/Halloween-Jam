using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace BattleV2.UI
{
    /// <summary>
    /// Submenú de ataques físicos.
    /// </summary>
    public sealed class AtkMenuPanel : BattlePanelBase, ICancelHandler
    {
        [Serializable]
        private struct AttackEntry
        {
            public string actionId;
            public Button button;
        }

        [SerializeField] private AttackEntry[] attacks = Array.Empty<AttackEntry>();
        [SerializeField] private Button backButton;
        [SerializeField] private Button defaultButton;

        public event Action<string> OnAttackChosen;
        public event Action OnBack;

        protected override void Awake()
        {
            base.Awake();
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

        public override void FocusFirst()
        {
            Button target = defaultButton;
            if (target == null && attacks != null && attacks.Length > 0)
            {
                target = attacks[0].button;
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
