using System;
using BattleV2.Charge;
using BattleV2.Core;
using BattleV2.Execution.TimedHits;
using UnityEngine;

namespace BattleV2.Actions
{
    [CreateAssetMenu(menuName = "Battle/Actions/KS1 Lunar Chain")]
    public class LunarChainAction : ScriptableObject, IAction, IActionProvider, ITimedHitAction, ITimedHitPhaseDamageAction
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

        private readonly struct DamageSnapshot
        {
            public DamageSnapshot(
                Ks1TimedHitProfile.Tier tier,
                int totalPhases,
                int baseDamagePerHit,
                float tierMultiplier,
                float chargeMultiplier,
                float attackStatContribution,
                int baseDamageValue)
            {
                Tier = tier;
                TotalPhases = totalPhases;
                BaseDamagePerHit = baseDamagePerHit;
                TierMultiplier = tierMultiplier;
                ChargeMultiplier = chargeMultiplier;
                AttackStatContribution = attackStatContribution;
                BaseDamageValue = baseDamageValue;
            }

            public Ks1TimedHitProfile.Tier Tier { get; }
            public int TotalPhases { get; }
            public int BaseDamagePerHit { get; }
            public float TierMultiplier { get; }
            public float ChargeMultiplier { get; }
            public float AttackStatContribution { get; }
            public int BaseDamageValue { get; }
        }

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

        public bool TryBuildPhaseDamagePlan(CombatantState actor, CombatContext context, int cpCharge, out TimedHitPhaseDamagePlan plan)
        {
            if (TryBuildDamageSnapshot(actor, context, cpCharge, out var snapshot))
            {
                plan = new TimedHitPhaseDamagePlan(
                    snapshot.BaseDamagePerHit,
                    minimumDamage,
                    snapshot.TierMultiplier,
                    snapshot.TotalPhases,
                    allowPartialOnMiss: true);
                return true;
            }

            plan = default;
            return false;
        }

        private bool TryBuildDamageSnapshot(CombatantState actor, CombatContext context, int cpCharge, out DamageSnapshot snapshot)
        {
            snapshot = default;

            if (timedHitProfile == null || actor == null || context?.Enemy == null)
            {
                return false;
            }

            var tier = timedHitProfile.GetTierForCharge(cpCharge);
            int totalPhases = Mathf.Max(1, tier.Hits);

            var stats = context.PlayerStats;
            float attackStat = stats.Physical;
            float scaledBaseDamage = baseDamage;
            if (attackPowerMultiplier != 0f)
            {
                scaledBaseDamage += attackStat * attackPowerMultiplier;
            }

            float chargeMultiplier = ComboPointScaling.GetDamageMultiplier(cpCharge);
            int baseDamagePerHit = Mathf.Max(minimumDamage, Mathf.RoundToInt(scaledBaseDamage * chargeMultiplier));
            float tierMultiplier = tier.DamageMultiplier > 0f ? tier.DamageMultiplier : 1f;

            snapshot = new DamageSnapshot(
                tier,
                totalPhases,
                baseDamagePerHit,
                tierMultiplier,
                chargeMultiplier,
                attackStat,
                baseDamage);
            return true;
        }

        public void Execute(CombatantState actor, CombatContext context, int cpCharge, TimedHitResult? timedResult, Action onComplete)
        {
            if (actor == null || context?.Enemy == null)
            {
                onComplete?.Invoke();
                return;
            }

            if (!TryBuildDamageSnapshot(actor, context, cpCharge, out var snapshot))
            {
                BattleLogger.Warn("KS1", "Lunar Chain executed without a valid timed-hit profile; no damage applied.");
                onComplete?.Invoke();
                return;
            }

            int totalHits = snapshot.TotalPhases;
            int hitsSucceeded = snapshot.TotalPhases;
            float perHitMultiplier = snapshot.TierMultiplier;
            bool damageResolvedExternally = false;
            int externalDamage = 0;

            if (timedResult.HasValue)
            {
                var raw = timedResult.Value;

                Debug.Log(
                    $"[KS1] Final timed-hit result → Judgment={raw.Judgment}, Mult={raw.DamageMultiplier:0.00}, Hits={raw.HitsSucceeded}/{raw.TotalHits}",
                    this);

                BattleLogger.Log(
                    "KS1",
                    $"LunarChain timed-hit -> hits {raw.HitsSucceeded}/{raw.TotalHits}, judgment={raw.Judgment}, mult {raw.DamageMultiplier:F2}, externalDamage={raw.TotalDamageApplied}, phasesResolved={raw.PhaseDamageApplied}");
                totalHits = raw.TotalHits > 0 ? raw.TotalHits : totalHits;
                hitsSucceeded = Mathf.Clamp(raw.HitsSucceeded, 0, totalHits);
                perHitMultiplier = raw.DamageMultiplier > 0f ? raw.DamageMultiplier : perHitMultiplier;
                damageResolvedExternally = raw.PhaseDamageApplied;
                externalDamage = raw.TotalDamageApplied;
            }
            else
            {
                hitsSucceeded = totalHits;
            }

            // Ensure we still deal some damage on miss/fail.
            if (!damageResolvedExternally && hitsSucceeded == 0)
            {
                hitsSucceeded = 1;
                // Miss multiplier: default to 0.8f if tier doesn't provide one.
                float missMult = snapshot.Tier.MissHitMultiplier > 0f ? snapshot.Tier.MissHitMultiplier : 0.8f;
                perHitMultiplier = missMult;
                BattleLogger.Warn("KS1", "Timed hit missed; applying fallback damage with miss multiplier.");
            }

            if (damageResolvedExternally)
            {
                if (externalDamage > 0)
                {
                    BattleLogger.Log(
                        "KS1",
                        $"Lunar Chain dealt {externalDamage} damage via phase hits ({hitsSucceeded}/{totalHits} hits) (Base {snapshot.BaseDamageValue}, AP {snapshot.AttackStatContribution:F1}, Mult {perHitMultiplier:F2}, ChargeMult {snapshot.ChargeMultiplier:F2}).");
                }
                else
                {
                    BattleLogger.Warn("KS1", $"Lunar Chain resolved with no damage ({hitsSucceeded}/{totalHits} hits).");
                }

                onComplete?.Invoke();
                return;
            }

            int totalDamage = 0;
            var enemy = context.Enemy;

            for (int i = 0; i < hitsSucceeded; i++)
            {
                int hitDamage = Mathf.RoundToInt(snapshot.BaseDamagePerHit * perHitMultiplier);
                totalDamage += hitDamage;
                enemy.TakeDamage(hitDamage);
            }

            if (hitsSucceeded > 0)
            {
                BattleLogger.Log(
                    "KS1",
                    $"Lunar Chain dealt {totalDamage} total damage ({hitsSucceeded}/{totalHits} hits) (Base {snapshot.BaseDamageValue}, AP {snapshot.AttackStatContribution:F1}, Mult {perHitMultiplier:F2}, ChargeMult {snapshot.ChargeMultiplier:F2}).");
            }
            else
            {
                BattleLogger.Warn("KS1", "Lunar Chain failed to connect (combo interrupted).");
            }

            onComplete?.Invoke();
        }
    }
}
