using System;
using HalloweenJam.Combat.Strategies;

namespace HalloweenJam.Combat
{
    /// <summary>
    /// Performs combat calculations and notifies listeners about the outcomes.
    /// </summary>
    public sealed class BattleActionResolver
    {
        public event Action<AttackResolutionContext> AttackResolved;

        public AttackResolutionContext ResolveAttack(ICombatEntity attacker, ICombatEntity defender)
        {
            if (attacker == null)
            {
                throw new ArgumentNullException(nameof(attacker));
            }

            if (defender == null)
            {
                throw new ArgumentNullException(nameof(defender));
            }

            var attackResult = attacker.AttackStrategy.Execute(attacker, defender);
            defender.ReceiveDamage(attackResult.Damage);

            var logMessage = $"{attacker.DisplayName} {attackResult.Description}";
            var context = new AttackResolutionContext(attacker, defender, attackResult, logMessage);

            AttackResolved?.Invoke(context);
            return context;
        }
    }
}

