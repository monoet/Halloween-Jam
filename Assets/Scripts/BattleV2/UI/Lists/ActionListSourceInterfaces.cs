using System.Collections.Generic;
using BattleV2.Core;

namespace BattleV2.UI.Lists
{
    public interface ISpellListSource
    {
        IReadOnlyList<ISpellRowData> GetSpellsFor(CombatantState actor, CombatContext context);
    }

    public interface IItemListSource
    {
        IReadOnlyList<IItemRowData> GetItemsFor(CombatantState actor, CombatContext context);
    }
}
