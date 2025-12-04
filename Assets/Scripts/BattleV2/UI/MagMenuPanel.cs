using System;
using System.Collections.Generic;
using BattleV2.Core;
using BattleV2.UI.Lists;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BattleV2.UI
{
    /// <summary>
    /// Submenú de hechizos con lista dinámica.
    /// </summary>
    public sealed class MagMenuPanel : BattlePanelBase, ICancelHandler
    {
        [SerializeField] private ActionListPopulator populator;
        [SerializeField] private MonoBehaviour spellSourceBehaviour;
        [SerializeField] private TMP_Text spCostHeader;
        [SerializeField] private TooltipUI tooltip;
        [SerializeField] private Button backButton;

        public event Action<string> OnSpellChosen;
        public event Action OnBack;

        private ISpellListSource SpellSource => spellSourceBehaviour as ISpellListSource;
        private IReadOnlyList<ISpellRowData> cachedRows = Array.Empty<ISpellRowData>();

        protected override void Awake()
        {
            base.Awake();
            backButton?.onClick.AddListener(HandleBack);
        }

        public void ShowFor(CombatantState actor, CombatContext context)
        {
            cachedRows = SpellSource != null ? SpellSource.GetSpellsFor(actor, context) : Array.Empty<ISpellRowData>();
            populator?.ShowSpells(cachedRows, HandleHover, HandleSubmit, HandleBlocked);
            tooltip?.Hide();
            UpdateHeader(null);
        }

        public override void FocusFirst()
        {
            populator?.FocusLast();
        }

        public void OnCancel(BaseEventData eventData)
        {
            HandleBack();
        }

        private void HandleHover(ISpellRowData data)
        {
            UpdateHeader(data);
            if (tooltip != null)
            {
                tooltip.Show(data != null ? data.Description : string.Empty);
            }
        }

        private void HandleSubmit(ISpellRowData data)
        {
            if (data == null)
            {
                return;
            }

            OnSpellChosen?.Invoke(data.Id);
        }

        private void HandleBlocked(ISpellRowData data)
        {
            if (tooltip != null)
            {
                string reason = data != null && !string.IsNullOrWhiteSpace(data.DisabledReason)
                    ? data.DisabledReason
                    : "No disponible";
                tooltip.Show(reason);
            }

            UIAudio.PlayBack();
        }

        private void HandleBack()
        {
            UIAudio.PlayBack();
            OnBack?.Invoke();
        }

        private void UpdateHeader(ISpellRowData data)
        {
            if (spCostHeader == null)
            {
                return;
            }

            if (data == null)
            {
                spCostHeader.text = string.Empty;
                return;
            }

            spCostHeader.text = $"SP: {Mathf.Max(0, data.SpCost)}";
        }
    }
}
