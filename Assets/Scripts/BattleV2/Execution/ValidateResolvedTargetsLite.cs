using System.Collections.Generic;
using BattleV2.Actions;
using BattleV2.Core;
using BattleV2.Targeting;

namespace BattleV2.Execution
{
    /// <summary>
    /// Validates already-resolved targets (no selection/policy); used for P2-lite shadow checks.
    /// </summary>
    public static class ValidateResolvedTargetsLite
    {
        public readonly struct ValidationResultLite
        {
            public ValidationResultLite(bool hasRecipients, TargetResolveFailReason failReason, CombatantState[] recipients, bool containsSelf)
            {
                HasRecipients = hasRecipients;
                FailReason = failReason;
                Recipients = recipients ?? System.Array.Empty<CombatantState>();
                ContainsSelf = containsSelf;

                // Enforce invariant: non-Ok => no recipients and HasRecipients=false.
                if (failReason != TargetResolveFailReason.Ok)
                {
                    HasRecipients = false;
                    Recipients = System.Array.Empty<CombatantState>();
                    ContainsSelf = false;
                }
                else if (!HasRecipients)
                {
                    Recipients = System.Array.Empty<CombatantState>();
                    ContainsSelf = false;
                }
            }

            public bool HasRecipients { get; }
            public TargetResolveFailReason FailReason { get; }
            public CombatantState[] Recipients { get; }
            public bool ContainsSelf { get; }
        }

        public static ValidationResultLite Validate(
            CombatantState attacker,
            BattleActionData action,
            IReadOnlyList<CombatantState> resolvedTargets,
            IReadOnlyList<CombatantState> sameSide = null,
            IReadOnlyList<CombatantState> opponents = null)
        {
            if (action == null)
            {
                return new ValidationResultLite(false, TargetResolveFailReason.NullAction, System.Array.Empty<CombatantState>(), false);
            }

            // Only interested in offensive single validation. Others are skipped.
            if (action.targetAudience != TargetAudience.Enemies || action.targetShape != TargetShape.Single)
            {
                return new ValidationResultLite(false, TargetResolveFailReason.NotOffensiveSingle, System.Array.Empty<CombatantState>(), false);
            }

            var recipients = TargetSnapshot.Snapshot(resolvedTargets);
            bool hasRecipients = recipients.Length > 0;

            if (!hasRecipients)
            {
                return new ValidationResultLite(false, TargetResolveFailReason.NoTargets, recipients, false);
            }

            bool containsNull = false;
            bool containsDuplicates = false;
            bool containsSelf = false;
            var seenIds = new System.Collections.Generic.HashSet<int>();

            for (int i = 0; i < recipients.Length; i++)
            {
                var r = recipients[i];
                if (r == null)
                {
                    containsNull = true;
                    continue;
                }

                if (attacker != null && r == attacker)
                {
                    containsSelf = true;
                }

                int id = r.GetInstanceID();
                if (!seenIds.Add(id))
                {
                    containsDuplicates = true;
                }
            }

            if (containsNull || containsDuplicates)
            {
                return new ValidationResultLite(false, TargetResolveFailReason.InvalidTargets, recipients, containsSelf);
            }

            if (containsSelf)
            {
                return new ValidationResultLite(false, TargetResolveFailReason.UnauthorizedSelf, recipients, true);
            }

            return new ValidationResultLite(true, TargetResolveFailReason.Ok, recipients, false);
        }
    }
}
