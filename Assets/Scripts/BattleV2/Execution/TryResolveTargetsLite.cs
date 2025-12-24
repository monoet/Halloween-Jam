using System.Collections.Generic;
using BattleV2.Actions;
using BattleV2.Core;
using BattleV2.Targeting;

namespace BattleV2.Execution
{
    /// <summary>
    /// Shadow-mode helper to resolve single offensive targets without side effects.
    /// </summary>
    public static class TryResolveTargetsLite
    {
        public readonly struct ResolvedTargetsLite
        {
            public ResolvedTargetsLite(bool hasRecipients, TargetResolveFailReason failReason, CombatantState[] recipients)
            {
                HasRecipients = hasRecipients;
                FailReason = failReason;
                Recipients = recipients ?? System.Array.Empty<CombatantState>();

                // Invariants: if fail != Ok, force no recipients/HasRecipients=false.
                if (failReason != TargetResolveFailReason.Ok)
                {
                    HasRecipients = false;
                    Recipients = System.Array.Empty<CombatantState>();
                }
                else if (!HasRecipients)
                {
                    // If HasRecipients was false but reason is Ok, normalize to no recipients.
                    Recipients = System.Array.Empty<CombatantState>();
                }
            }

            public bool HasRecipients { get; }
            public TargetResolveFailReason FailReason { get; }
            public CombatantState[] Recipients { get; }
        }

        public static ResolvedTargetsLite Resolve(
            CombatantState attacker,
            BattleActionData action,
            IReadOnlyList<CombatantState> sameSide,
            IReadOnlyList<CombatantState> opponents)
        {
            if (action == null)
            {
                return new ResolvedTargetsLite(false, TargetResolveFailReason.NullAction, System.Array.Empty<CombatantState>());
            }

            if (action.targetAudience != TargetAudience.Enemies || action.targetShape != TargetShape.Single)
            {
                return new ResolvedTargetsLite(false, TargetResolveFailReason.NotOffensiveSingle, System.Array.Empty<CombatantState>());
            }

            var candidates = TargetSnapshot.Snapshot(opponents);
            if (candidates.Length == 0)
            {
                return new ResolvedTargetsLite(false, TargetResolveFailReason.NoTargets, System.Array.Empty<CombatantState>());
            }

            var recipients = new List<CombatantState>(candidates.Length);
            for (int i = 0; i < candidates.Length; i++)
            {
                var c = candidates[i];
                if (c == null) continue;
                if (!c.IsAlive) continue;
                if (attacker != null && c == attacker) continue;
                recipients.Add(c);
                break; // single
            }

            if (recipients.Count == 0)
            {
                return new ResolvedTargetsLite(false, TargetResolveFailReason.SelfOnly, System.Array.Empty<CombatantState>());
            }

            return new ResolvedTargetsLite(true, TargetResolveFailReason.Ok, recipients.ToArray());
        }
    }
}
