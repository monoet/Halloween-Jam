using System;
using System.Collections.Generic;
using BattleV2.Core;

namespace BattleV2.Targeting
{
    public interface ITargetResolver
    {
        TargetSet Resolve(TargetContext context);
    }

    public sealed class TargetResolverRegistry
    {
        private readonly Dictionary<TargetShape, ITargetResolver> resolvers = new();

        public void Register(TargetShape shape, ITargetResolver resolver)
        {
            if (resolver == null)
            {
                return;
            }

            resolvers[shape] = resolver;
        }

        public TargetSet Resolve(TargetContext context)
        {
            if (context.Action == null)
            {
                return TargetSet.None;
            }

            var query = context.Query;
            if (resolvers.TryGetValue(query.Shape, out var resolver))
            {
                return resolver.Resolve(context);
            }

            return TargetSet.None;
        }
    }

    internal sealed class SingleTargetResolver : ITargetResolver
    {
        public TargetSet Resolve(TargetContext context)
        {
            switch (context.Query.Audience)
            {
                case TargetAudience.Self:
                    return TargetSet.Single(context.Origin != null ? context.Origin.GetInstanceID() : 0);
                case TargetAudience.Allies:
                    return TargetSet.Single(FindFirstAlive(context.Allies));
                case TargetAudience.Enemies:
                default:
                    return TargetSet.Single(FindFirstAlive(context.Enemies));
            }
        }

        private static int FindFirstAlive(IReadOnlyList<CombatantState> list)
        {
            if (list == null)
            {
                return 0;
            }

            for (int i = 0; i < list.Count; i++)
            {
                var combatant = list[i];
                if (combatant != null && combatant.IsAlive)
                {
                    return combatant.GetInstanceID();
                }
            }

            return 0;
        }
    }

    internal sealed class GroupTargetResolver : ITargetResolver
    {
        public TargetSet Resolve(TargetContext context)
        {
            switch (context.Query.Audience)
            {
                case TargetAudience.Self:
                    return TargetSet.Group(CreateSingleList(context.Origin));
                case TargetAudience.Allies:
                    return TargetSet.Group(CollectAlive(context.Allies));
                case TargetAudience.Enemies:
                default:
                    return TargetSet.Group(CollectAlive(context.Enemies));
            }
        }

        private static IReadOnlyList<int> CreateSingleList(CombatantState combatant)
        {
            if (combatant == null)
            {
                return Array.Empty<int>();
            }

            return new[] { combatant.GetInstanceID() };
        }

        private static IReadOnlyList<int> CollectAlive(IReadOnlyList<CombatantState> list)
        {
            if (list == null || list.Count == 0)
            {
                return Array.Empty<int>();
            }

            var ids = new List<int>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                var combatant = list[i];
                if (combatant != null && combatant.IsAlive)
                {
                    ids.Add(combatant.GetInstanceID());
                }
            }

            return ids;
        }
    }
}
