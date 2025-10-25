using System.Collections.Generic;
using BattleV2.Actions;
using BattleV2.Core;

namespace BattleV2.Providers
{
    public class BattleActionContext
    {
        public CombatantState Player;
        public CharacterRuntime PlayerRuntime;
        public CombatantState Enemy;
        public CharacterRuntime EnemyRuntime;
        public IReadOnlyList<BattleActionData> AvailableActions;
        public CombatContext Context;
        public int MaxCpCharge;
        public FinalStats PlayerStats;
        public FinalStats EnemyStats;
    }
}
