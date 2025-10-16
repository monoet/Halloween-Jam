using System;
using HalloweenJam.Combat.Strategies;
using UnityEngine;

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

            var attackResult = Resolve(attacker, defender);
            defender.ReceiveDamage(attackResult.Damage);

            var logMessage = $"{attacker.DisplayName} {attackResult.Description}";
            var context = new AttackResolutionContext(attacker, defender, attackResult, logMessage);

            AttackResolved?.Invoke(context);
            return context;
        }

        private AttackResult Resolve(ICombatEntity attacker, ICombatEntity defender)
        {
            if (TryResolveWithAction(attacker, defender, out var actionResult))
            {
                return actionResult;
            }

            var baseResult = attacker.AttackStrategy.Execute(attacker, defender);
            return TryResolveWithStats(attacker, defender, baseResult);
        }

        private bool TryResolveWithAction(ICombatEntity attacker, ICombatEntity defender, out AttackResult result)
        {
            result = default;

            if (!(attacker is RuntimeCombatEntity attackerRuntimeEntity) ||
                !(defender is RuntimeCombatEntity defenderRuntimeEntity))
            {
                return false;
            }

            var action = attackerRuntimeEntity.DefaultAction;
            var attackerRuntime = attackerRuntimeEntity.CharacterRuntime;
            var defenderRuntime = defenderRuntimeEntity.CharacterRuntime;

            if (action == null || attackerRuntime == null || defenderRuntime == null)
            {
                return false;
            }

            var attackerState = attackerRuntimeEntity.CombatantState;
            var defenderState = defenderRuntimeEntity.CombatantState;

            attackerState?.EnsureInitialized(attackerRuntime);
            defenderState?.EnsureInitialized(defenderRuntime);

            if (action.CpCost > 0)
            {
                attackerState?.SpendCP(action.CpCost);
            }

            bool isHit = UnityEngine.Random.value <= Mathf.Clamp01(action.HitChance);
            if (!isHit)
            {
                result = new AttackResult(0, $"misses with {action.ActionName}.");
                if (action.CpGain > 0)
                {
                    attackerState?.AddCP(action.CpGain);
                }

                return true;
            }

            bool isCrit = ActionResolver.RollCritStatic(attackerRuntime);
            float power = ActionResolver.CalculateActionPower(attackerRuntime, action, logDetails: false);
            if (isCrit)
            {
                power *= 1.5f;
            }

            int damage = Mathf.Max(1, Mathf.CeilToInt(power));

            if (action.CpGain > 0)
            {
                attackerState?.AddCP(action.CpGain);
            }

            var description = string.IsNullOrWhiteSpace(action.ActionName)
                ? $"strikes for {damage} damage."
                : $"uses {action.ActionName} for {damage} damage.";

            if (isCrit)
            {
                description = $"lands a critical hit with {action.ActionName} for {damage} damage.";
            }

            result = new AttackResult(damage, description);
            return true;
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
