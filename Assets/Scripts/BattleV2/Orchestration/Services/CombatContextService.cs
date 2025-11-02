using System.Collections.Generic;
using BattleV2.Actions;
using BattleV2.Core;

namespace BattleV2.Orchestration.Services
{
    public sealed class CombatContextService
    {
        private readonly BattleConfig config;
        private readonly ActionCatalog actionCatalog;

        public CombatContextService(BattleConfig config, ActionCatalog actionCatalog)
        {
            this.config = config;
            this.actionCatalog = actionCatalog;
        }

        public CombatContextUpdate RefreshPlayerContext(
            CombatContext currentContext,
            CombatantState player,
            CharacterRuntime playerRuntime,
            CombatantState currentEnemy,
            CharacterRuntime currentEnemyRuntime,
            IReadOnlyList<CombatantState> enemies)
        {
            var resolvedEnemy = EnsureEnemy(currentEnemy, enemies);
            var resolvedEnemyRuntime = ResolveRuntime(resolvedEnemy, currentEnemyRuntime);
            var resolvedPlayerRuntime = ResolveRuntime(player, playerRuntime);

            var services = currentContext != null
                ? currentContext.Services
                : (config != null ? config.services : new BattleServices());

            var refreshedContext = new CombatContext(
                player,
                resolvedEnemy,
                resolvedPlayerRuntime,
                resolvedEnemyRuntime,
                services,
                actionCatalog);

            return new CombatContextUpdate(
                refreshedContext,
                resolvedEnemy,
                resolvedEnemyRuntime,
                resolvedPlayerRuntime);
        }

        public CombatContext CreateEnemyContext(
            CombatantState attacker,
            CombatantState target,
            CombatContext playerContext,
            CharacterRuntime fallbackPlayerRuntime)
        {
            var services = playerContext != null
                ? playerContext.Services
                : (config != null ? config.services : new BattleServices());

            return new CombatContext(
                attacker,
                target,
                ResolveRuntime(attacker, attacker?.CharacterRuntime),
                ResolveRuntime(target, target?.CharacterRuntime ?? fallbackPlayerRuntime),
                services,
                actionCatalog);
        }

        public CharacterRuntime ResolveRuntime(CombatantState combatant, CharacterRuntime overrideRuntime)
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

        private static CombatantState EnsureEnemy(CombatantState current, IReadOnlyList<CombatantState> roster)
        {
            if (current != null && current.IsAlive)
            {
                return current;
            }

            if (roster == null)
            {
                return null;
            }

            for (int i = 0; i < roster.Count; i++)
            {
                var candidate = roster[i];
                if (candidate != null && candidate.IsAlive)
                {
                    return candidate;
                }
            }

            return null;
        }
    }

    public readonly struct CombatContextUpdate
    {
        public CombatContextUpdate(
            CombatContext context,
            CombatantState enemy,
            CharacterRuntime enemyRuntime,
            CharacterRuntime playerRuntime)
        {
            Context = context;
            Enemy = enemy;
            EnemyRuntime = enemyRuntime;
            PlayerRuntime = playerRuntime;
        }

        public CombatContext Context { get; }
        public CombatantState Enemy { get; }
        public CharacterRuntime EnemyRuntime { get; }
        public CharacterRuntime PlayerRuntime { get; }
    }
}
