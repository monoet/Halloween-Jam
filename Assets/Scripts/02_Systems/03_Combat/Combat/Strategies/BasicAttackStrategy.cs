using UnityEngine;

namespace HalloweenJam.Combat.Strategies
{
    [CreateAssetMenu(menuName = "Legacy/Combat/Attack Strategies/Basic Attack")]
    public sealed class BasicAttackStrategy : AttackStrategyBase
    {
        [SerializeField] private int minimumDamage = 1;

        public override AttackResult Execute(ICombatEntity attacker, ICombatEntity defender)
        {
            var damage = Mathf.Max(minimumDamage, attacker.AttackPower);
            var description = $"attacks for {damage} damage.";
            return new AttackResult(damage, description);
        }
    }
}
