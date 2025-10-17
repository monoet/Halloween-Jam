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
        public BattleServices Services { get; }
        public ActionCatalog Catalog { get; }

        public CombatContext(CombatantState player, CombatantState enemy, BattleServices services, ActionCatalog catalog)
        {
            Player = player;
            Enemy = enemy;
            Services = services;
            Catalog = catalog;
        }
    }
}
