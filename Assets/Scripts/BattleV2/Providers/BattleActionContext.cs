using System.Collections.Generic;
using BattleV2.Actions;
using BattleV2.Core;

namespace BattleV2.Providers
{
    public class BattleActionContext
    {
        public CombatantState Player;
        public CombatantState Enemy;
        public IReadOnlyList<ActionData> AvailableActions;
        public CombatContext Context;
    }
}
