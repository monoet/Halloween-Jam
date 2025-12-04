using System.Collections.Generic;
using BattleV2.Core;

namespace BattleV2.UI.Lists
{
    internal static class ActionListSourceUtils
    {
        public static HashSet<string> BuildAllowedSet(CombatantState actor)
        {
            if (actor == null)
            {
                return null;
            }

            var allowedIds = actor.AllowedActionIds;
            if (allowedIds == null || allowedIds.Count == 0)
            {
                return null;
            }

            var set = new HashSet<string>();
            for (int i = 0; i < allowedIds.Count; i++)
            {
                var id = allowedIds[i];
                if (!string.IsNullOrWhiteSpace(id))
                {
                    set.Add(id);
                }
            }

            return set;
        }
    }
}
