using System;
using BattleV2.Charge;
using BattleV2.Core;
using UnityEngine;

namespace BattleV2.Actions
{
    [CreateAssetMenu(menuName = "Battle/Actions/KS2 Critical Burst")]
    public class CriticalBurstAction : ScriptableObject, IAction, IActionProvider
    {
        [SerializeField] private string actionId = "ks2_critical_burst";
        [SerializeField] private int costSp;
        [SerializeField] private int costCp;
        [SerializeField] private int baseDamage;
        [SerializeField] private float attackPowerMultiplier = 1f;
        [SerializeField] private int minimumDamage = 1;
        [SerializeField] private ChargeProfile chargeProfile;
        [SerializeField] private float baseMultiplier = 1.5f;
        [SerializeField] private float multiplierPerCp = 0.1f;

        public string Id => actionId;
        public int CostSP => costSp;
        public int CostCP => costCp;
        public ChargeProfile ChargeProfile => chargeProfile;

        public IAction Get() => this;

        public bool CanExecute(CombatantState actor, CombatContext context, int cpCharge)
        {
            if (actor == null || context?.Enemy == null)
            {
                return false;
            }

            if (!context.Enemy.IsAlive)
            {
                return false;
            }

            int totalCpCost = costCp + Mathf.Max(0, cpCharge);
            return actor.CurrentSP >= costSp && actor.CurrentCP >= totalCpCost;
        }

        public void Execute(CombatantState actor, CombatContext context, int cpCharge, TimedHitResult? timedResult, Action onComplete)
        {
            if (actor == null || context?.Enemy == null)
            {
                onComplete?.Invoke();
                return;
            }

            if (costSp > 0 && !actor.SpendSP(costSp))
            {
                BattleLogger.Warn("KS2", "Not enough SP to cast KS2 Critical Burst.");
                onComplete?.Invoke();
                return;
            }

            int totalCpCost = costCp + Mathf.Max(0, cpCharge);
            if (totalCpCost > 0 && !actor.SpendCP(totalCpCost))
            {
                BattleLogger.Warn("KS2", "Not enough CP to cast KS2 Critical Burst.");
                onComplete?.Invoke();
                return;
            }

            float multiplier = baseMultiplier + multiplierPerCp * Mathf.Max(0, totalCpCost);
            float cpMultiplier = ComboPointScaling.GetDamageMultiplier(cpCharge);

            float scaledDamage = baseDamage;
            var stats = context != null ? context.PlayerStats : default;
            if (attackPowerMultiplier != 0f)
            {
                scaledDamage += stats.Physical * attackPowerMultiplier;
            }

            int finalDamage = Mathf.Max(minimumDamage, Mathf.RoundToInt(scaledDamage * multiplier * cpMultiplier));

            BattleLogger.Log(
                "KS2",
                $"Critical Burst hits for {finalDamage} damage (Base {baseDamage}, AP {stats.Physical:F1}, Mult {multiplier:F2}, ChargeMult {cpMultiplier:F2}).");

            // Critical strike (placeholder): apply boosted damage directly.
            context.Enemy.TakeDamage(finalDamage);

            onComplete?.Invoke();
        }
    }
}
