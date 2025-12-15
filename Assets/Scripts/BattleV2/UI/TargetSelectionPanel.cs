using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace BattleV2.UI
{
    /// <summary>
    /// Selector de objetivos basado en botones (self/ally/enemy).
    /// </summary>
    public sealed class TargetSelectionPanel : BattlePanelBase, ICancelHandler
    {
        [Serializable]
        private struct TargetEntry
        {
            public int targetId;
            public Button button;
        }

        [SerializeField] private TargetEntry[] targets = Array.Empty<TargetEntry>();
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button defaultButton;

        public event Action<int> OnTargetSelected;
        public event Action OnCancelRequested;

        protected override void Awake()
        {
            base.Awake();
            if (targets != null)
            {
                for (int i = 0; i < targets.Length; i++)
                {
                    var entry = targets[i];
                    if (entry.button == null)
                    {
                        continue;
                    }

                    var id = entry.targetId;
                    entry.button.onClick.AddListener(() => OnTargetSelected?.Invoke(id));
                }
            }

            cancelButton?.onClick.AddListener(() => OnCancelRequested?.Invoke());
        }

        public override void FocusFirst()
        {
            Button target = defaultButton;
            if (target == null && targets != null && targets.Length > 0)
            {
                target = targets[0].button;
            }

            if (target != null)
            {
                EventSystem.current?.SetSelectedGameObject(target.gameObject);
            }
        }

        public void OnCancel(BaseEventData eventData)
        {
            UIAudio.PlayBack();
            OnCancelRequested?.Invoke();
        }
    }
}
