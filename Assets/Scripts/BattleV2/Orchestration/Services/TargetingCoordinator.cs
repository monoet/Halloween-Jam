using System.Threading.Tasks;
using System;
using System.Collections.Generic;

using System.Threading.Tasks;
using BattleV2.Actions;
using BattleV2.Core;
using BattleV2.Orchestration.Events;
using BattleV2.Targeting;

namespace BattleV2.Orchestration.Services
{
    public interface ITargetingCoordinator
    {
        Task<TargetResolutionResult> ResolveAsync(
            CombatantState origin,
            BattleActionData action,
            TargetSourceType sourceType,
            CombatantState fallback,
            IReadOnlyList<CombatantState> allies,
            IReadOnlyList<CombatantState> enemies);
    }

    /// <summary>
    /// Stub que usa resolvers existentes sin UI avanzada. Expandiremos con highlights/eventos.
    /// </summary>
    public sealed class TargetingCoordinator : ITargetingCoordinator
    {
        private readonly TargetResolverRegistry resolverRegistry;
        private readonly IBattleEventBus eventBus;
        private readonly List<CombatantState> scratchTargets = new();

        public TargetingCoordinator(TargetResolverRegistry resolverRegistry, IBattleEventBus eventBus)
        {
            this.resolverRegistry = resolverRegistry;
            this.eventBus = eventBus;
        }

        public Task<TargetResolutionResult> ResolveAsync(
            CombatantState origin,
            BattleActionData action,
            TargetSourceType sourceType,
            CombatantState fallback,
            IReadOnlyList<CombatantState> allies,
            IReadOnlyList<CombatantState> enemies)
        {
            var query = ResolveQuery(origin, allies, enemies);
            var context = new TargetContext(origin, action, query, sourceType, allies, enemies);

            TargetSet set = TargetSet.None;
            if (resolverRegistry != null)
            {
                set = resolverRegistry.Resolve(context);
            }

            if (set.IsEmpty)
            {
                set = EnsureFallbackSet(sourceType, fallback, allies, enemies);
            }

            scratchTargets.Clear();
            PopulateTargets(set, allies, enemies, scratchTargets);

            if (!set.IsEmpty)
            {
                eventBus?.Publish(new TargetHighlightEvent(origin, set));
            }

            var targetsCopy = scratchTargets.ToArray();
            scratchTargets.Clear();
            return Task.FromResult(new TargetResolutionResult(set, targetsCopy));
        }

        private static TargetQuery ResolveQuery(CombatantState origin, IReadOnlyList<CombatantState> allies, IReadOnlyList<CombatantState> enemies)
        {
            if (origin != null && Contains(allies, origin))
            {
                return TargetQuery.EnemiesSingle;
            }

            if (origin != null && Contains(enemies, origin))
            {
                return TargetQuery.AlliesSingle;
            }

            return TargetQuery.EnemiesSingle;
        }

        private static bool Contains(IReadOnlyList<CombatantState> list, CombatantState value)
        {
            if (list == null)
            {
                return false;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == value)
                {
                    return true;
                }
            }

            return false;
        }

        private static TargetSet EnsureFallbackSet(
            TargetSourceType sourceType,
            CombatantState fallback,
            IReadOnlyList<CombatantState> allies,
            IReadOnlyList<CombatantState> enemies)
        {
            CombatantState fallbackCombatant = fallback;
            if (fallbackCombatant == null || !fallbackCombatant.IsAlive)
            {
                fallbackCombatant = sourceType == TargetSourceType.Manual
                    ? FirstAlive(enemies)
                    : FirstAlive(allies);
            }

            if (fallbackCombatant == null)
            {
                return TargetSet.None;
            }

            return TargetSet.Single(fallbackCombatant.GetInstanceID());
        }

        private static CombatantState FirstAlive(IReadOnlyList<CombatantState> list)
        {
            if (list == null)
            {
                return null;
            }

            for (int i = 0; i < list.Count; i++)
            {
                var combatant = list[i];
                if (combatant != null && combatant.IsAlive)
                {
                    return combatant;
                }
            }

            return null;
        }

        private static void PopulateTargets(
            TargetSet set,
            IReadOnlyList<CombatantState> allies,
            IReadOnlyList<CombatantState> enemies,
            List<CombatantState> buffer)
        {
            if (set.IsEmpty)
            {
                return;
            }

            if (set.IsGroup && set.Ids != null)
            {
                for (int i = 0; i < set.Ids.Count; i++)
                {
                    var target = FindById(set.Ids[i], allies, enemies);
                    if (target != null && !buffer.Contains(target))
                    {
                        buffer.Add(target);
                    }
                }
            }
            else if (set.TryGetSingle(out var id))
            {
                var target = FindById(id, allies, enemies);
                if (target != null)
                {
                    buffer.Add(target);
                }
            }
        }

        private static CombatantState FindById(int id, IReadOnlyList<CombatantState> allies, IReadOnlyList<CombatantState> enemies)
        {
            if (id == 0)
            {
                return null;
            }

            var target = SearchList(allies, id);
            return target ?? SearchList(enemies, id);
        }

        private static CombatantState SearchList(IReadOnlyList<CombatantState> list, int id)
        {
            if (list == null)
            {
                return null;
            }

            for (int i = 0; i < list.Count; i++)
            {
                var combatant = list[i];
                if (combatant != null && combatant.GetInstanceID() == id)
                {
                    return combatant;
                }
            }

            return null;
        }
    }

    public readonly struct TargetResolutionResult
    {
        public TargetResolutionResult(TargetSet targetSet, CombatantState[] targets)
        {
            TargetSet = targetSet;
            Targets = targets ?? Array.Empty<CombatantState>();
        }

        public TargetSet TargetSet { get; }
        public IReadOnlyList<CombatantState> Targets { get; }
    }
}
