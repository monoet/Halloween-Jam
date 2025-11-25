using System;
using UnityEngine;
using UnityEngine.UI;

namespace BattleV2.UI
{
    /// <summary>
    /// Selector de objetivos basado en botones (self/ally/enemy).
    /// </summary>
    public sealed class TargetSelectionPanel : MonoBehaviour
    {
        [Serializable]
        private struct TargetEntry
        {
            public int targetId;
            public Button button;
        }

        [SerializeField] private TargetEntry[] targets = Array.Empty<TargetEntry>();
        [SerializeField] private Button cancelButton;

        public event Action<int> OnTargetSelected;
        public event Action OnCancel;

        private void Awake()
        {
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

            cancelButton?.onClick.AddListener(() => OnCancel?.Invoke());
        }
    }
}
