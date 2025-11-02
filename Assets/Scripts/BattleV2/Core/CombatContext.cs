using BattleV2.Actions;

namespace BattleV2.Core
{
    /// <summary>
    /// Shared context passed around during battle execution.
    /// </summary>
    public sealed class CombatContext
    {
        public CombatantState Player { get; }
        public CombatantState Enemy { get; }
        public CharacterRuntime PlayerRuntime { get; }
        public CharacterRuntime EnemyRuntime { get; }
        public FinalStats PlayerStats => PlayerRuntime != null ? PlayerRuntime.Final : default;
        public FinalStats EnemyStats => EnemyRuntime != null ? EnemyRuntime.Final : default;
        public BattleServices Services { get; }
        public ActionCatalog Catalog { get; }

        public CombatContext(
            CombatantState player,
            CombatantState enemy,
            CharacterRuntime playerRuntime,
            CharacterRuntime enemyRuntime,
            BattleServices services,
            ActionCatalog catalog)
        {
            Player = player;
            Enemy = enemy;
            PlayerRuntime = playerRuntime;
            EnemyRuntime = enemyRuntime;
            Services = services;
            Catalog = catalog;
        }

        public CombatContext WithEnemy(CombatantState enemy, CharacterRuntime enemyRuntime)
        {
            return new CombatContext(
                Player,
                enemy,
                PlayerRuntime,
                enemyRuntime,
                Services,
                Catalog);
        }
    }
}
