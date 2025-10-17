using System;
using BattleV2.Core;
using HalloweenJam.Combat;
using UnityEngine;

namespace BattleV2.Actions
{
    [CreateAssetMenu(menuName = "Battle/Actions/Item Heal")]
    public class ItemHealAction : ScriptableObject, IAction, IActionProvider
    {
        [Header("Base Info")]
        [SerializeField] private string actionId = "item_heal";
        [SerializeField] private int healAmount = 20;
        [SerializeField] private int costSp;
        [SerializeField] private int costCp;
        [SerializeField] private bool consumable = true;

        public string Id => actionId;
        public int CostSP => costSp;
        public int CostCP => costCp;

        public IAction Get() => this;

        public bool CanExecute(CombatantState actor, CombatContext context, int cpCharge)
        {
            if (actor == null)
            {
                return false;
            }

            int totalCpCost = costCp + Mathf.Max(0, cpCharge);
            return actor.CurrentSP >= costSp && actor.CurrentCP >= totalCpCost && actor.IsAlive;
        }

        public void Execute(CombatantState actor, CombatContext context, int cpCharge, Action onComplete)
        {
            if (actor == null)
            {
                BattleLogger.Warn("ItemHeal", "Actor missing in ItemHealAction.");
                onComplete?.Invoke();
                return;
            }

            if (costSp > 0 && !actor.SpendSP(costSp))
            {
                BattleLogger.Warn("ItemHeal", $"{actor.name} lacks SP to use healing item.");
                onComplete?.Invoke();
                return;
            }

            int totalCpCost = costCp + Mathf.Max(0, cpCharge);
            if (totalCpCost > 0 && !actor.SpendCP(totalCpCost))
            {
                BattleLogger.Warn("ItemHeal", $"{actor.name} lacks CP to use healing item.");
                onComplete?.Invoke();
                return;
            }

            int healValue = healAmount; // TODO: scale heal with cpCharge if desired.

            BattleLogger.Log("ItemHeal", $"{actor.name} uses healing item for {healValue} HP. (CP Charge: {cpCharge})");

            actor.Heal(healValue);

            if (consumable)
            {
                // TODO: Hook con inventario para descontar item
                BattleLogger.Log("ItemHeal", "Item consumed.");
            }

            // Hook opcional: context.Services?.SpawnVFX("HealFX", actor.Position);
            onComplete?.Invoke();
        }
    }
}
