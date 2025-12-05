using BattleV2.Core;
using System.Collections.Generic;

namespace BattleV2.Core.Services
{
    public enum CombatRelation
    {
        Self,
        Ally,
        Enemy,
        Neutral
    }

    public interface ICombatSideService
    {
        CombatRelation GetRelation(CombatantState a, CombatantState b);
        bool IsAlly(CombatantState a, CombatantState b);
        bool IsEnemy(CombatantState a, CombatantState b);
    }
}
