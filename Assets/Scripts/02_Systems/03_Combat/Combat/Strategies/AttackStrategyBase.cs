using UnityEngine;

namespace HalloweenJam.Combat.Strategies
{
    public abstract class AttackStrategyBase : ScriptableObject, IAttackStrategy
    {
        public abstract AttackResult Execute(ICombatEntity attacker, ICombatEntity defender);
    }
}
