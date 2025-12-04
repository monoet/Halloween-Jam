using System.Collections.Generic;
using BattleV2.Actions;
using BattleV2.Core;
using UnityEngine;

namespace BattleV2.UI.Lists
{
    /// <summary>
    /// Adapta un ActionCatalog (lista Magic) a datos de fila, respetando el orden serializado y AllowedActionIds.
    /// </summary>
    public sealed class CatalogSpellListSource : MonoBehaviour, ISpellListSource
    {
        [SerializeField] private ActionCatalog catalog;
        [SerializeField] private string insufficientSpReason = "SP insuficiente";

        public IReadOnlyList<ISpellRowData> GetSpellsFor(CombatantState actor, CombatContext context)
        {
            var resolvedCatalog = catalog != null ? catalog : context?.Catalog;
            var spells = resolvedCatalog != null ? resolvedCatalog.Magic : null;
            if (spells == null)
            {
                return System.Array.Empty<ISpellRowData>();
            }

            var allowed = ActionListSourceUtils.BuildAllowedSet(actor);
            var result = new List<ISpellRowData>(spells.Count);

            for (int i = 0; i < spells.Count; i++)
            {
                var data = spells[i];
                if (data == null || (allowed != null && !allowed.Contains(data.id)))
                {
                    continue;
                }

                int spCost = Mathf.Max(0, data.costSP);
                bool enabled = actor != null ? actor.CurrentSP >= spCost : true;
                string disabledReason = enabled ? null : insufficientSpReason;
                string description = !string.IsNullOrWhiteSpace(data.description) ? data.description : data.displayName;

                result.Add(new SpellRowData(
                    data.id,
                    data.displayName,
                    description,
                    enabled,
                    disabledReason,
                    spCost,
                    data.elementIcon));
            }

            return result;
        }
    }
}
