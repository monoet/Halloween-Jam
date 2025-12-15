using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace BattleV2.UI
{
    /// <summary>
    /// Menú raíz: Attack, Magic, Item, Defend, Flee.
    /// </summary>
    public sealed class ActionMenuPanel : BattlePanelBase
    {
        [SerializeField] private Button attackButton;
        [SerializeField] private Button magicButton;
        [SerializeField] private Button itemButton;
        [SerializeField] private Button defendButton;
        [SerializeField] private Button fleeButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button defaultButton;

        public event Action OnAttack;
        public event Action OnMagic;
        public event Action OnItem;
        public event Action OnDefend;
        public event Action OnFlee;
        public event Action OnClose;

        protected override void Awake()
        {
            base.Awake();
            attackButton?.onClick.AddListener(() => OnAttack?.Invoke());
            magicButton?.onClick.AddListener(() => OnMagic?.Invoke());
            itemButton?.onClick.AddListener(() => OnItem?.Invoke());
            defendButton?.onClick.AddListener(() => OnDefend?.Invoke());
            fleeButton?.onClick.AddListener(() => OnFlee?.Invoke());
            closeButton?.onClick.AddListener(() => OnClose?.Invoke());
        }

        public override void FocusFirst()
        {
            var target = defaultButton != null ? defaultButton : attackButton;
            if (target != null)
            {
                EventSystem.current?.SetSelectedGameObject(target.gameObject);
            }
        }
    }
}
