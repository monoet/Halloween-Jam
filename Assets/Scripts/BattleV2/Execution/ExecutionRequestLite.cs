using System.Collections.Generic;
using BattleV2.Actions;
using BattleV2.Core;

namespace BattleV2.Execution
{
    /// <summary>
    /// Immutable DTO for P2-lite shadow logging of execution inputs.
    /// </summary>
    public readonly struct ExecutionRequestLite
    {
        public ExecutionRequestLite(
            int executionId,
            CombatantState attacker,
            BattleActionData action,
            IReadOnlyList<CombatantState> recipients,
            IReadOnlyList<CombatantState> sameSide,
            IReadOnlyList<CombatantState> opponents,
            TargetResolveFailReason failReason)
        {
            ExecutionId = executionId;
            Attacker = attacker;
            Action = action;
            Recipients = TargetSnapshot.Snapshot(recipients);
            SameSide = TargetSnapshot.Snapshot(sameSide);
            Opponents = TargetSnapshot.Snapshot(opponents);
            FailReason = failReason;
        }

        public int ExecutionId { get; }
        public CombatantState Attacker { get; }
        public BattleActionData Action { get; }
        public CombatantState[] Recipients { get; }
        public CombatantState[] SameSide { get; }
        public CombatantState[] Opponents { get; }
        public TargetResolveFailReason FailReason { get; }
    }
}
