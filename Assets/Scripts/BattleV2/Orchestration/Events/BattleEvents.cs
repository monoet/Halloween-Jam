using System;
using BattleV2.Actions;
using BattleV2.Core;
using BattleV2.Providers;
using BattleV2.Targeting;

namespace BattleV2.Orchestration.Events
{
    public readonly struct ActionStartedEvent
    {
        public ActionStartedEvent(CombatantState actor, BattleSelection selection)
        {
            Actor = actor;
            Selection = selection;
        }

        public CombatantState Actor { get; }
        public BattleSelection Selection { get; }
    }

    public readonly struct ActionCompletedEvent
    {
        public ActionCompletedEvent(CombatantState actor, BattleSelection selection)
        {
            Actor = actor;
            Selection = selection;
        }

        public CombatantState Actor { get; }
        public BattleSelection Selection { get; }
    }

    public readonly struct HitResolvedEvent
    {
        public HitResolvedEvent(CombatantState attacker, CombatantState target, BattleActionData action, int damage)
        {
            Attacker = attacker;
            Target = target;
            Action = action;
            Damage = damage;
        }

        public CombatantState Attacker { get; }
        public CombatantState Target { get; }
        public BattleActionData Action { get; }
        public int Damage { get; }
    }

    public readonly struct TargetHighlightEvent
    {
        public TargetHighlightEvent(CombatantState actor, TargetSet targets)
        {
            Actor = actor;
            Targets = targets;
        }

        public CombatantState Actor { get; }
        public TargetSet Targets { get; }
    }
}
