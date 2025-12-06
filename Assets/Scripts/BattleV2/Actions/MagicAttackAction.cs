using System;
using System.Collections.Generic;
using BattleV2.Charge;
using BattleV2.Core;
using HalloweenJam.Combat;
using BattleV2.Providers;
using UnityEngine;

namespace BattleV2.Actions
{
    [CreateAssetMenu(menuName = "Battle/Actions/Magic Attack")]
    public class MagicAttackAction : ScriptableObject, IAction, IActionProvider, IActionMultiTarget
    {
        [Header("Base Info")]
        [SerializeField] private string actionId = "magic_attack";
        [SerializeField] private int costSp = 5;
        [SerializeField] private int costCp;
        [SerializeField] private int baseDamage = 15;
        [SerializeField] private float magicPowerMultiplier = 1f;
        [SerializeField] private int minimumDamage = 1;
        [SerializeField] private string element = "Fire";
        [SerializeField] private ChargeProfile chargeProfile;

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
            // Legacy single-target path falls back to enemy.
            if (context?.Enemy == null)
            {
                BattleLogger.Warn("MagicAttack", "No target for magic attack.");
                onComplete?.Invoke();
                return;
            }

            if (costSp > 0 && !actor.SpendSP(costSp))
            {
                BattleLogger.Warn("MagicAttack", $"{actor.name} tried to cast {element} without enough SP.");
                onComplete?.Invoke();
                return;
            }

            int totalCpCost = costCp + Mathf.Max(0, cpCharge);
            if (totalCpCost > 0 && !actor.SpendCP(totalCpCost))
            {
                BattleLogger.Warn("MagicAttack", $"{actor.name} tried to cast {element} without enough CP.");
                onComplete?.Invoke();
                return;
            }

            float scaledDamage = baseDamage;
            var stats = context != null ? context.PlayerStats : default;
            if (magicPowerMultiplier != 0f)
            {
                scaledDamage += stats.MagicPower * magicPowerMultiplier;
            }

            float cpMultiplier = ComboPointScaling.GetDamageMultiplier(cpCharge);

            int totalDamage = Mathf.Max(minimumDamage, Mathf.RoundToInt(scaledDamage * cpMultiplier));

            BattleLogger.Log(
                "MagicAttack",
                $"{actor.name} casts {element} dealing {totalDamage} damage (Base {baseDamage}, MP {stats.MagicPower:F1}, Charge {cpCharge}, Mult {cpMultiplier:F2}).");
            context.Enemy.TakeDamage(totalDamage);

            // TODO: context.Services?.SpawnVFX($"{element}SpellFX", context.Enemy.Position);
            // TODO: Add animations or sound hooks

            onComplete?.Invoke();
        }

        public void ExecuteMulti(
            CombatantState actor,
            CombatContext context,
            IReadOnlyList<CombatantState> targets,
            BattleSelection selection,
            Action onComplete)
        {
            if (targets == null || targets.Count == 0)
            {
                BattleLogger.Warn("MagicAttack", "No targets for magic attack.");
                onComplete?.Invoke();
                return;
            }

            // Note: recursos ya se cobraron una vez en el pipeline.
            var stats = context != null ? context.PlayerStats : default;
            float scaledDamageBase = baseDamage;
            if (magicPowerMultiplier != 0f)
            {
                scaledDamageBase += stats.MagicPower * magicPowerMultiplier;
            }

            float cpMultiplier = ComboPointScaling.GetDamageMultiplier(selection.CpCharge);
            int totalDamageBase = Mathf.Max(minimumDamage, Mathf.RoundToInt(scaledDamageBase * cpMultiplier));
            float timedMultiplier = 1f;
            if (selection.TimedHitResult.HasValue)
            {
                timedMultiplier = Mathf.Max(0f, selection.TimedHitResult.Value.DamageMultiplier);
            }

            int finalDamagePerTarget = Mathf.Max(minimumDamage, Mathf.RoundToInt(totalDamageBase * timedMultiplier));

            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null || !target.IsAlive)
                {
                    continue;
                }

                BattleLogger.Log(
                    "MagicAttack",
                    $"{actor.name} casts {element} dealing {finalDamagePerTarget} damage to {target.name} (Targets All).");
                target.TakeDamage(finalDamagePerTarget);
            }

            onComplete?.Invoke();
        }
    }
}
