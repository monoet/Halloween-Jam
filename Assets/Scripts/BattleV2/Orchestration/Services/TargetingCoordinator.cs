using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using BattleV2.Actions;
using BattleV2.Core;
using BattleV2.Orchestration.Events;
using BattleV2.Targeting;
using BattleV2.Targeting.Policies;
using UnityEngine;

namespace BattleV2.Orchestration.Services
{
    public interface ITargetingCoordinator
    {
        void SetInteractor(ITargetSelectionInteractor interactor);
        void SetPolicy(ITargetResolutionPolicy policy);
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
        private ITargetSelectionInteractor selectionInteractor;
        private ITargetResolutionPolicy resolutionPolicy;

        public TargetingCoordinator(
            TargetResolverRegistry resolverRegistry,
            IBattleEventBus eventBus,
            ITargetSelectionInteractor selectionInteractor = null,
            ITargetResolutionPolicy resolutionPolicy = null)
        {
            this.resolverRegistry = resolverRegistry;
            this.eventBus = eventBus;
            this.selectionInteractor = selectionInteractor;
            this.resolutionPolicy = resolutionPolicy ?? new DefaultResolutionPolicy();
        }

        public void SetInteractor(ITargetSelectionInteractor interactor)
        {
            selectionInteractor = interactor;
        }

        public void SetPolicy(ITargetResolutionPolicy policy)
        {
            resolutionPolicy = policy ?? new DefaultResolutionPolicy();
        }

        public async Task<TargetResolutionResult> ResolveAsync(
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

            if (sourceType == TargetSourceType.Manual)
            {
                if (selectionInteractor != null)
                {
                    try
                    {
                        BattleDiagnostics.Log("Targeting", "Invoking Manual Selection...", origin);
                        set = await selectionInteractor.SelectAsync(context, set);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[TargetingCoordinator] Manual selection failed ({ex.Message}). Falling back to auto resolution.");
                    }
                }
                else
                {
                    BattleDiagnostics.Log("Targeting", "Manual selection requested but NO INTERACTOR registered. Skipping UI.", origin);
                }
            }

            scratchTargets.Clear();
            PopulateTargets(set, allies, enemies, scratchTargets);

            if (!set.IsEmpty)
            {
                eventBus?.Publish(new TargetHighlightEvent(origin, set));
            }

            var targetsCopy = scratchTargets.ToArray();
            scratchTargets.Clear();

            var initialStatus = set.IsBack ? TargetResolutionStatus.Back : TargetResolutionStatus.Confirmed;
            if (set.IsEmpty && !set.IsBack) initialStatus = TargetResolutionStatus.Cancelled;

            var tempResult = new TargetResolutionResult(set, targetsCopy, initialStatus);
            var finalStatus = resolutionPolicy.Interpret(tempResult, action);

            return new TargetResolutionResult(set, targetsCopy, finalStatus);
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

    public enum TargetResolutionStatus
    {
        None = 0,
        Confirmed,
        Back,
        Cancelled
    }

    public readonly struct TargetResolutionResult
    {
        public TargetResolutionResult(TargetSet targetSet, CombatantState[] targets, TargetResolutionStatus status)
        {
            TargetSet = targetSet;
            Targets = targets ?? Array.Empty<CombatantState>();
            Status = status;
        }

        public TargetResolutionResult(TargetSet targetSet, CombatantState[] targets) 
            : this(targetSet, targets, TargetResolutionStatus.Confirmed) { }

        public TargetSet TargetSet { get; }
        public IReadOnlyList<CombatantState> Targets { get; }
        public TargetResolutionStatus Status { get; }
    }
}
