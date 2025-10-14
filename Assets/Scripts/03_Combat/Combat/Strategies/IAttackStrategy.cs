using HalloweenJam.Combat;

namespace HalloweenJam.Combat.Strategies
{
    public interface IAttackStrategy
    {
        AttackResult Execute(ICombatEntity attacker, ICombatEntity defender);
    }
}
