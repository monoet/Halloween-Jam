using System;
using BattleV2.Actions;
using System.Collections.Generic;
using BattleV2.Core;
using BattleV2.Execution;
using BattleV2.Providers;
using BattleV2.Targeting;

namespace BattleV2.Orchestration.Events
{
    public readonly struct ActionStartedEvent
    {
        public ActionStartedEvent(CombatantState actor, BattleSelection selection, IReadOnlyList<CombatantState> targets)
        {
            Actor = actor;
            Selection = selection;
            Targets = targets;
        }

        public CombatantState Actor { get; }
        public BattleSelection Selection { get; }
        public IReadOnlyList<CombatantState> Targets { get; }
    }

    /// <summary>
    /// Identifica una ejecución completa de acción (incluye efectos disparados).
    /// ExecutionId se genera al commit y permanece estable durante toda la cadena.
    /// </summary>
    public readonly struct ActionCompletedEvent
    {
        public ActionCompletedEvent(int executionId, CombatantState actor, BattleSelection selection, IReadOnlyList<CombatantState> targets, bool isTriggered = false, ActionJudgment? judgment = null)
        {
            ExecutionId = executionId;
            Actor = actor;
            Selection = selection;
            Targets = targets;
            IsTriggered = isTriggered;
            Judgment = judgment;
        }

        public int ExecutionId { get; }
        public CombatantState Actor { get; }
        public BattleSelection Selection { get; }
        public IReadOnlyList<CombatantState> Targets { get; }
        public bool IsTriggered { get; }
        public ActionJudgment? Judgment { get; }
    }

    public readonly struct AttackFrameEvent
    {
        public AttackFrameEvent(CombatantState actor, CombatantState target, BattleActionData action, int frameIndex, int frameCount)
        {
            Actor = actor;
            Target = target;
            Action = action;
            FrameIndex = frameIndex;
            FrameCount = frameCount;
        }

        public CombatantState Actor { get; }
        public CombatantState Target { get; }
        public BattleActionData Action { get; }
        public int FrameIndex { get; }
        public int FrameCount { get; }
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

    public readonly struct CombatantDefeatedEvent
    {
        public CombatantDefeatedEvent(CombatantState combatant, CombatantState killer)
        {
            Combatant = combatant;
            Killer = killer;
        }

        public CombatantState Combatant { get; }
        public CombatantState Killer { get; }
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
