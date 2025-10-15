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

            var baseResult = attacker.AttackStrategy.Execute(attacker, defender);
            var attackResult = TryResolveWithStats(attacker, defender, baseResult);
            defender.ReceiveDamage(attackResult.Damage);

            var logMessage = $"{attacker.DisplayName} {attackResult.Description}";
            var context = new AttackResolutionContext(attacker, defender, attackResult, logMessage);

            AttackResolved?.Invoke(context);
            return context;
        }

        private static AttackResult TryResolveWithStats(ICombatEntity attacker, ICombatEntity defender, AttackResult fallback)
        {
            if (TryGetCharacterRuntime(attacker, out var attackerRuntime) &&
                TryGetCharacterRuntime(defender, out var defenderRuntime))
            {
                var statsResult = StatsDamageCalculator.CalculateBasicAttack(attackerRuntime, defenderRuntime);
                var description = string.IsNullOrWhiteSpace(fallback.Description) ? statsResult.Description : fallback.Description;
                return new AttackResult(statsResult.Damage, description);
            }

            return fallback;
        }

        private static bool TryGetCharacterRuntime(ICombatEntity entity, out global::CharacterRuntime runtime)
        {
            if (entity is RuntimeCombatEntity runtimeEntity)
            {
                runtime = runtimeEntity.CharacterRuntime;
                return runtime != null;
            }

            runtime = null;
            return false;
        }
    }
}
