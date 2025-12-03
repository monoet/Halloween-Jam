using System;
using System.Collections.Generic;
using BattleV2.Actions;
using BattleV2.Targeting;
using UnityEngine;

namespace BattleV2.Orchestration
{
    /// <summary>
    /// Represents a player's selection in progress (Draft).
    /// Used to separate the "Build" phase (selecting action, targets) from the "Commit" phase (consuming CP, executing).
    /// </summary>
    public readonly struct SelectionDraft
    {
        public CombatantState Actor { get; }
        public BattleActionData Action { get; }
        public int CpIntent { get; }
        public string OriginMenu { get; }
        public TargetSet Targets { get; }
        public bool IsCommitted { get; }
        public int Version { get; }

        public SelectionDraft(
            CombatantState actor,
            BattleActionData action,
            int cpIntent,
            string originMenu,
            TargetSet targets,
            bool isCommitted,
            int version)
        {
            Actor = actor;
            Action = action;
            CpIntent = cpIntent;
            OriginMenu = originMenu;
            Targets = targets;
            IsCommitted = isCommitted;
            Version = version;
        }

        public bool IsValid => Actor != null && Action != null;
        public bool HasTargets => !Targets.IsEmpty;

        public static SelectionDraft Create(CombatantState actor, BattleActionData action, int cpIntent, string originMenu)
        {
            return new SelectionDraft(actor, action, cpIntent, originMenu, TargetSet.None, false, 1);
        }

        public SelectionDraft WithTargets(TargetSet targets)
        {
            return new SelectionDraft(Actor, Action, CpIntent, OriginMenu, targets, IsCommitted, Version + 1);
        }

        public SelectionDraft MarkCommitted()
        {
            return new SelectionDraft(Actor, Action, CpIntent, OriginMenu, Targets, true, Version + 1);
        }

        public SelectionDraft ClearTargets()
        {
            return new SelectionDraft(Actor, Action, CpIntent, OriginMenu, TargetSet.None, false, Version + 1);
        }

        public static SelectionDraft Empty => default;
    }
}
