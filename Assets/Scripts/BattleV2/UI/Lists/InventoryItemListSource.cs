using System.Collections.Generic;
using BattleV2.Actions;
using BattleV2.Core;
using UnityEngine;

namespace BattleV2.UI.Lists
{
    /// <summary>
    /// Fuente simple de ítems: toma ActionCatalog.Items y cruza con inventario serializado (placeholder) para qty.
    /// </summary>
    public sealed class InventoryItemListSource : MonoBehaviour, IItemListSource
    {
        [System.Serializable]
        private struct ItemStock
        {
            public string itemId;
            public int quantity;
        }

        [SerializeField] private ActionCatalog catalog;
        [SerializeField] private List<ItemStock> inventory = new List<ItemStock>();
        [SerializeField] private string outOfStockReason = "Sin existencias";

        public IReadOnlyList<IItemRowData> GetItemsFor(CombatantState actor, CombatContext context)
        {
            var resolvedCatalog = catalog != null ? catalog : context?.Catalog;
            var items = resolvedCatalog != null ? resolvedCatalog.Items : null;
            if (items == null)
            {
                return System.Array.Empty<IItemRowData>();
            }

            var allowed = ActionListSourceUtils.BuildAllowedSet(actor);
            var result = new List<IItemRowData>(items.Count);

            for (int i = 0; i < items.Count; i++)
            {
                var data = items[i];
                if (data == null || (allowed != null && !allowed.Contains(data.id)))
                {
                    continue;
                }

                int qty = ResolveQuantity(data.id);
                bool enabled = qty > 0;
                string disabledReason = enabled ? null : outOfStockReason;
                string description = !string.IsNullOrWhiteSpace(data.description) ? data.description : data.displayName;

                result.Add(new ItemRowData(
                    data.id,
                    data.displayName,
                    description,
                    enabled,
                    disabledReason,
                    qty));
            }

            return result;
        }

        private int ResolveQuantity(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId) || inventory == null)
            {
                return 0;
            }

            for (int i = 0; i < inventory.Count; i++)
            {
                var stock = inventory[i];
                if (string.Equals(stock.itemId, itemId))
                {
                    return Mathf.Max(0, stock.quantity);
                }
            }

            return 0;
        }
    }
}
