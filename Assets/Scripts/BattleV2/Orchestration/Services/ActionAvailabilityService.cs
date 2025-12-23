using System;
using System.Collections.Generic;
using BattleV2.Actions;
using BattleV2.Core;

namespace BattleV2.Orchestration.Services
{
    public static class ActionAvailabilityService
    {
        public static IReadOnlyList<BattleActionData> BuildAvailableFor(
            ActionCatalog catalog,
            CombatantState actor,
            CombatContext context,
            bool strictAllowedIds)
        {
            var available = catalog?.BuildAvailableFor(actor, context);
            return FilterAllowedIds(available, actor?.AllowedActionIds, strictAllowedIds, actor);
        }

        public static IReadOnlyList<BattleActionData> FilterAllowedIds(
            IReadOnlyList<BattleActionData> available,
            IReadOnlyList<string> allowedIds,
            bool strictAllowedIds,
            CombatantState logContext)
        {
            if (available == null)
            {
                return Array.Empty<BattleActionData>();
            }

            if (allowedIds == null || allowedIds.Count == 0)
            {
                return available;
            }

            var lookup = new HashSet<string>(allowedIds, StringComparer.OrdinalIgnoreCase);
            var filtered = new List<BattleActionData>(available.Count);
            for (int i = 0; i < available.Count; i++)
            {
                var candidate = available[i];
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.id))
                {
                    continue;
                }

                if (lookup.Contains(candidate.id))
                {
                    filtered.Add(candidate);
                }
            }

            if (filtered.Count > 0)
            {
                return filtered;
            }

            if (strictAllowedIds)
            {
                BattleLogger.Warn("ActionAvailability", $"Actor '{logContext?.name ?? "(null)"}' has an action filter but no matching actions in catalog.");
                return Array.Empty<BattleActionData>();
            }

            return available;
        }
    }
}

