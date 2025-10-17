using System;
using BattleV2.Charge;
using BattleV2.Core;
using HalloweenJam.Combat;
using UnityEngine;

namespace BattleV2.Actions
{
    [CreateAssetMenu(menuName = "Battle/Actions/Simple Attack")]
    public class SimpleAttackAction : ScriptableObject, IAction, IActionProvider
    {
        [SerializeField] private string actionId = "simple_attack";
        [SerializeField] private int costSp;
        [SerializeField] private int costCp;
        [SerializeField] private int damage = 5;
        [SerializeField] private int cpGain = 1;
        [SerializeField] private ChargeProfile chargeProfile;

        public string Id => actionId;
        public int CostSP => costSp;
        public int CostCP => costCp;
        public ChargeProfile ChargeProfile => chargeProfile;

        public IAction Get() => this;

        public bool CanExecute(CombatantState actor, CombatContext context, int cpCharge)
        {
            if (actor == null || context == null)
            {
                return false;
            }

            if (context.Enemy == null || !context.Enemy.IsAlive)
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
                BattleLogger.Warn("Action", "SimpleAttackAction executed with missing enemy.");
                onComplete?.Invoke();
                return;
            }

            if (costSp > 0 && !actor.SpendSP(costSp))
            {
                BattleLogger.Warn("Action", $"{actor.name} lacks SP ({actor.CurrentSP}/{costSp}) for SimpleAttack.");
                onComplete?.Invoke();
                return;
            }

            int totalCpCost = costCp + Mathf.Max(0, cpCharge);
            if (totalCpCost > 0 && !actor.SpendCP(totalCpCost))
            {
                BattleLogger.Warn("Action", $"{actor.name} lacks CP ({actor.CurrentCP}/{totalCpCost}) for SimpleAttack.");
                onComplete?.Invoke();
                return;
            }

            int totalDamage = damage; // TODO: add scaling per cpCharge if desired.

            BattleLogger.Log("Action", $"SimpleAttack dealing {totalDamage} damage (CP Charge: {cpCharge}).");
            context.Enemy.TakeDamage(totalDamage);

            if (cpGain > 0 && costCp == 0 && cpCharge == 0)
            {
                actor.AddCP(cpGain);
            }

            // TODO: integrate animations via context.Services.GetAnimatorFor(actor)
            onComplete?.Invoke();
        }
    }
}
