using System;
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

        public string Id => actionId;
        public int CostSP => costSp;
        public int CostCP => costCp;

        public IAction Get() => this;

        public bool CanExecute(CombatantState actor, CombatContext context)
        {
            if (actor == null || context == null)
            {
                return false;
            }

            return actor.CurrentSP >= costSp && actor.CurrentCP >= costCp && context.Enemy != null && context.Enemy.IsAlive;
        }

        public void Execute(CombatantState actor, CombatContext context, Action onComplete)
        {
            if (context?.Enemy == null)
            {
                BattleLogger.Warn("Action", "SimpleAttackAction executed with missing enemy.");
                onComplete?.Invoke();
                return;
            }

            BattleLogger.Log("Action", $"SimpleAttack dealing {damage} damage.");
            context.Enemy.TakeDamage(damage);

            if (cpGain > 0)
            {
                actor.AddCP(cpGain);
            }

            // TODO: integrate animations via context.Services.GetAnimatorFor(actor)
            onComplete?.Invoke();
        }
    }
}
