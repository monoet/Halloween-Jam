using System;
using UnityEngine;
using UnityEngine.UI;

namespace BattleV2.UI
{
    /// <summary>
    /// Menú raíz: Attack, Magic, Item, Defend, Flee.
    /// </summary>
    public sealed class ActionMenuPanel : MonoBehaviour
    {
        [SerializeField] private Button attackButton;
        [SerializeField] private Button magicButton;
        [SerializeField] private Button itemButton;
        [SerializeField] private Button defendButton;
        [SerializeField] private Button fleeButton;
        [SerializeField] private Button closeButton;

        public event Action OnAttack;
        public event Action OnMagic;
        public event Action OnItem;
        public event Action OnDefend;
        public event Action OnFlee;
        public event Action OnClose;

        private void Awake()
        {
            attackButton?.onClick.AddListener(() => OnAttack?.Invoke());
            magicButton?.onClick.AddListener(() => OnMagic?.Invoke());
            itemButton?.onClick.AddListener(() => OnItem?.Invoke());
            defendButton?.onClick.AddListener(() => OnDefend?.Invoke());
            fleeButton?.onClick.AddListener(() => OnFlee?.Invoke());
            closeButton?.onClick.AddListener(() => OnClose?.Invoke());
        }
    }
}
