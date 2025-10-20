using System;
using BattleV2.Charge;
using BattleV2.Core;
using UnityEngine;

namespace BattleV2.Actions
{
    [CreateAssetMenu(menuName = "Battle/Actions/KS1 Lunar Chain")]
    public class LunarChainAction : ScriptableObject, IAction, IActionProvider, ITimedHitAction
    {
        [SerializeField] private string actionId = "ks1_lunar_chain";
        [SerializeField] private int costSp;
        [SerializeField] private int costCp;
        [SerializeField] private int baseDamage;
        [SerializeField] private float attackPowerMultiplier = 1f;
        [SerializeField] private int minimumDamage = 1;
        [SerializeField] private ChargeProfile chargeProfile;
        [SerializeField] private Ks1TimedHitProfile timedHitProfile;

        public string Id => actionId;
        public int CostSP => costSp;
        public int CostCP => costCp;
        public ChargeProfile ChargeProfile => chargeProfile;
        public Ks1TimedHitProfile TimedHitProfile => timedHitProfile;

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

        public void Execute(CombatantState actor, CombatContext context, int cpCharge, Action onComplete)
        {
            if (actor == null || context?.Enemy == null)
            {
                onComplete?.Invoke();
                return;
            }

            if (costSp > 0 && !actor.SpendSP(costSp))
            {
                BattleLogger.Warn("KS1", "Not enough SP to cast KS1 Lunar Chain.");
                onComplete?.Invoke();
                return;
            }

            int totalCpCost = costCp + Mathf.Max(0, cpCharge);
            if (totalCpCost > 0 && !actor.SpendCP(totalCpCost))
            {
                BattleLogger.Warn("KS1", "Not enough CP to cast KS1 Lunar Chain.");
                onComplete?.Invoke();
                return;
            }

            var tier = timedHitProfile != null ? timedHitProfile.GetTierForCharge(cpCharge) : default;
            int hits = Mathf.Max(0, tier.Hits);
            float damageMultiplier = tier.DamageMultiplier > 0f ? tier.DamageMultiplier : 1f;
            float scaledBaseDamage = baseDamage;
            var stats = context != null ? context.PlayerStats : default;
            if (attackPowerMultiplier != 0f)
            {
                scaledBaseDamage += stats.Physical * attackPowerMultiplier;
            }

            float chargeMultiplier = ComboPointScaling.GetDamageMultiplier(cpCharge);
            int damagePerHit = Mathf.Max(minimumDamage, Mathf.RoundToInt(scaledBaseDamage * chargeMultiplier));

            int totalDamage = 0;

            for (int i = 0; i < hits; i++)
            {
                int hitDamage = Mathf.RoundToInt(damagePerHit * damageMultiplier);
                totalDamage += hitDamage;
                context.Enemy.TakeDamage(hitDamage);
            }

            if (hits > 0)
            {
                BattleLogger.Log(
                    "KS1",
                    $"Lunar Chain dealt {totalDamage} total damage over {hits} hit(s) (Base {baseDamage}, AP {stats.Physical:F1}, Mult {damageMultiplier:F2}, ChargeMult {chargeMultiplier:F2}).");
            }

            int refund = Mathf.Clamp(hits, 0, tier.RefundMax);
            if (refund > 0)
            {
                actor.AddCP(refund);
            }

            onComplete?.Invoke();
        }
    }
}
