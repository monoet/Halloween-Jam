using BattleV2.Core;

namespace BattleV2.Core.Services
{
    /// <summary>
    /// Single source of truth for combat side. Uses TeamId on CombatantState.
    /// </summary>
    public sealed class CombatSideService : ICombatSideService
    {
        public CombatRelation GetRelation(CombatantState a, CombatantState b)
        {
            if (a == null || b == null)
            {
                return CombatRelation.Neutral;
            }

            if (ReferenceEquals(a, b))
            {
                return CombatRelation.Self;
            }

            int teamA = a.TeamId;
            int teamB = b.TeamId;

            if (teamA == teamB)
            {
                return CombatRelation.Ally;
            }

            return CombatRelation.Enemy;
        }

        public bool IsAlly(CombatantState a, CombatantState b) => GetRelation(a, b) == CombatRelation.Ally;

        public bool IsEnemy(CombatantState a, CombatantState b) => GetRelation(a, b) == CombatRelation.Enemy;
    }
}
