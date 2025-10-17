using System;
using BattleV2.Core;
using HalloweenJam.Combat;
using UnityEngine;

namespace BattleV2.Actions
{
    [CreateAssetMenu(menuName = "Battle/Actions/Magic Attack")]
    public class MagicAttackAction : ScriptableObject, IAction, IActionProvider
    {
        [Header("Base Info")]
        [SerializeField] private string actionId = "magic_attack";
        [SerializeField] private int costSp = 5;
        [SerializeField] private int costCp;
        [SerializeField] private int damage = 15;
        [SerializeField] private string element = "Fire";

        public string Id => actionId;
        public int CostSP => costSp;
        public int CostCP => costCp;

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

            int totalDamage = damage; // TODO: scale damage with cpCharge if desired.

            BattleLogger.Log("MagicAttack", $"{actor.name} casts {element} dealing {totalDamage} damage! (CP Charge: {cpCharge})");
            context.Enemy.TakeDamage(totalDamage);

            // TODO: context.Services?.SpawnVFX($"{element}SpellFX", context.Enemy.Position);
            // TODO: Add animations or sound hooks

            onComplete?.Invoke();
        }
    }
}
