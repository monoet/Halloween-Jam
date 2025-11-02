using BattleV2.Actions;
using BattleV2.Core;
using UnityEngine;

namespace BattleV2.Orchestration.Services
{
    public sealed class CombatantRosterCoordinator
    {
        private readonly CombatantRosterService rosterService;
        private readonly CombatContextService contextService;

        public CombatantRosterCoordinator(CombatantRosterService rosterService, CombatContextService contextService)
        {
            this.rosterService = rosterService ?? new CombatantRosterService();
            this.contextService = contextService;
            Snapshot = RosterSnapshot.Empty;
        }

        public RosterSnapshot Snapshot { get; private set; }

        public RosterCoordinatorResult RebuildRoster(
            BattleRosterConfig config,
            bool preservePlayerVitals,
            bool preserveEnemyVitals,
            CombatContext currentContext)
        {
            Snapshot = rosterService.Rebuild(config, preservePlayerVitals, preserveEnemyVitals);

            var contextUpdate = contextService != null
                ? contextService.RefreshPlayerContext(
                    currentContext,
                    Snapshot.Player,
                    Snapshot.PlayerRuntime,
                    Snapshot.Enemy,
                    Snapshot.EnemyRuntime,
                    Snapshot.Enemies)
                : default;

            return new RosterCoordinatorResult(Snapshot, contextUpdate);
        }

        public RosterCoordinatorResult RefreshAfterDeath(CombatantState combatant, CombatContext currentContext)
        {
            Snapshot = rosterService.RefreshAfterDeath(combatant);

            var contextUpdate = contextService != null
                ? contextService.RefreshPlayerContext(
                    currentContext,
                    Snapshot.Player,
                    Snapshot.PlayerRuntime,
                    Snapshot.Enemy,
                    Snapshot.EnemyRuntime,
                    Snapshot.Enemies)
                : default;

            return new RosterCoordinatorResult(Snapshot, contextUpdate);
        }

        public CombatContextUpdate RefreshContext(CombatContext currentContext)
        {
            if (contextService == null)
            {
                return default;
            }

            return contextService.RefreshPlayerContext(
                currentContext,
                Snapshot.Player,
                Snapshot.PlayerRuntime,
                Snapshot.Enemy,
                Snapshot.EnemyRuntime,
                Snapshot.Enemies);
        }

        public CombatContext CreateEnemyContext(
            CombatantState attacker,
            CombatantState target,
            CombatContext playerContext,
            CharacterRuntime fallbackPlayerRuntime,
            ActionCatalog actionCatalog,
            BattleConfig configFallback)
        {
            if (contextService != null)
            {
                return contextService.CreateEnemyContext(attacker, target, playerContext, fallbackPlayerRuntime);
            }

            var services = playerContext != null
                ? playerContext.Services
                : (configFallback != null ? configFallback.services : new BattleServices());

            return new CombatContext(
                attacker,
                target,
                ResolveRuntime(attacker, attacker?.CharacterRuntime),
                ResolveRuntime(target, target?.CharacterRuntime ?? fallbackPlayerRuntime),
                services,
                actionCatalog);
        }

        public void Cleanup()
        {
            rosterService?.Cleanup();
            Snapshot = RosterSnapshot.Empty;
        }

        private CharacterRuntime ResolveRuntime(CombatantState combatant, CharacterRuntime overrideRuntime)
        {
            if (overrideRuntime != null)
            {
                return overrideRuntime;
            }

            if (combatant == null)
            {
                return null;
            }

            if (combatant.CharacterRuntime != null)
            {
                return combatant.CharacterRuntime;
            }

            return combatant.GetComponent<CharacterRuntime>();
        }
    }

    public readonly struct RosterCoordinatorResult
    {
        public RosterCoordinatorResult(RosterSnapshot snapshot, CombatContextUpdate contextUpdate)
        {
            Snapshot = snapshot;
            ContextUpdate = contextUpdate;
        }

        public RosterSnapshot Snapshot { get; }
        public CombatContextUpdate ContextUpdate { get; }
    }
}
