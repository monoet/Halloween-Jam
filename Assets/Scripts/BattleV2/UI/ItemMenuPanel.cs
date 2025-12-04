using System;
using System.Collections.Generic;
using BattleV2.Core;
using BattleV2.UI.Lists;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BattleV2.UI
{
    /// <summary>
    /// Submenú de ítems con lista dinámica y cantidades.
    /// </summary>
    public sealed class ItemMenuPanel : BattlePanelBase, ICancelHandler
    {
        [SerializeField] private ActionListPopulator populator;
        [SerializeField] private MonoBehaviour itemSourceBehaviour;
        [SerializeField] private TooltipUI tooltip;
        [SerializeField] private Button backButton;

        public event Action<string> OnItemChosen;
        public event Action OnBack;

        private IItemListSource ItemSource => itemSourceBehaviour as IItemListSource;
        private IReadOnlyList<IItemRowData> cachedRows = Array.Empty<IItemRowData>();

        protected override void Awake()
        {
            base.Awake();
            backButton?.onClick.AddListener(HandleBack);
        }

        public void ShowFor(CombatantState actor, CombatContext context)
        {
            cachedRows = ItemSource != null ? ItemSource.GetItemsFor(actor, context) : Array.Empty<IItemRowData>();
            populator?.ShowItems(cachedRows, HandleHover, HandleSubmit, HandleBlocked);
            tooltip?.Hide();
        }

        public override void FocusFirst()
        {
            populator?.FocusFirstRow(preferEnabled: true);
        }

        public void OnCancel(BaseEventData eventData)
        {
            HandleBack();
        }

        private void HandleHover(IItemRowData data)
        {
            if (tooltip != null)
            {
                tooltip.Show(data != null ? data.Description : string.Empty);
            }
        }

        private void HandleSubmit(IItemRowData data)
        {
            if (data == null || data.Quantity <= 0 || !data.IsEnabled)
            {
                HandleBlocked(data);
                return;
            }

            OnItemChosen?.Invoke(data.Id);
        }

        private void HandleBlocked(IItemRowData data)
        {
            if (tooltip != null && data != null && !string.IsNullOrWhiteSpace(data.DisabledReason))
            {
                tooltip.Show(data.DisabledReason);
            }

            UIAudio.PlayBack();
        }

        private void HandleBack()
        {
            UIAudio.PlayBack();
            OnBack?.Invoke();
        }
    }
}
