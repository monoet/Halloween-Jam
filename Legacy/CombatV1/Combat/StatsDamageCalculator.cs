using HalloweenJam.Combat.Strategies;
using UnityEngine;

namespace HalloweenJam.Combat
{
    /// <summary>
    /// Shared helper that translates CharacterRuntime stats into combat damage.
    /// Encapsulates the math so BattleActionResolver (and eventually custom strategies)
    /// can remain slim.
    /// </summary>
    public static class StatsDamageCalculator
    {
        private const float DefenseMitigationFactor = 0.5f; // Allows partial defense contribution without nullifying damage.

        public static AttackResult CalculateBasicAttack(global::CharacterRuntime attacker, global::CharacterRuntime defender)
        {
            if (attacker == null || defender == null)
            {
                return new AttackResult(0, "flails without effect.");
            }

            FinalStats attackerStats = attacker.Final;
            FinalStats defenderStats = defender.Final;

            float attackPower = Mathf.Max(0f, attackerStats.Physical);
            float defensePower = Mathf.Max(0f, defenderStats.PhysDefense);
            float variance = Random.Range(0.9f, 1.1f);

            float mitigated = Mathf.Max(0f, attackPower - defensePower * DefenseMitigationFactor);
            int damage = Mathf.Max(1, Mathf.RoundToInt(mitigated * variance));

            string description = $"strikes for {damage} damage.";
            return new AttackResult(damage, description);
        }
    }
}
