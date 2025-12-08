using BattleV2.Core;

namespace BattleV2.Marks
{
    /// <summary>
    /// Single mark slot stored on the combatant. Source of truth for active mark state.
    /// </summary>
    [System.Serializable]
    public struct MarkSlot
    {
        public MarkSlot(
            MarkDefinition definition,
            CombatantState appliedBy,
            int appliedAtOwnerTurnCounter,
            int remainingTurns)
        {
            Definition = definition;
            AppliedBy = appliedBy;
            AppliedAtOwnerTurnCounter = appliedAtOwnerTurnCounter;
            RemainingTurns = remainingTurns;
            MarkId = ResolveKey(definition);
        }

        public static MarkSlot Empty => default;

        public MarkDefinition Definition { get; }
        public CombatantState AppliedBy { get; }
        public int AppliedAtOwnerTurnCounter { get; }
        public int RemainingTurns { get; }
        public string MarkId { get; }

        public bool HasValue => Definition != null && !string.IsNullOrEmpty(MarkId);

        public MarkSlot WithRemainingTurns(int turns)
        {
            return new MarkSlot(Definition, AppliedBy, AppliedAtOwnerTurnCounter, turns);
        }

        private static string ResolveKey(MarkDefinition definition)
        {
            if (definition == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(definition.id))
            {
                return definition.id;
            }

            return definition.name;
        }
    }
}
