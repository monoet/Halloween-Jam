using System;
using BattleV2.Providers;

namespace BattleV2.Marks
{
    public readonly struct ReactionKey : IEquatable<ReactionKey>
    {
        public ReactionKey(string markElementId, string incomingElementId, string axisSubtype)
        {
            MarkElementId = markElementId ?? string.Empty;
            IncomingElementId = incomingElementId ?? string.Empty;
            AxisSubtype = axisSubtype ?? string.Empty;
        }

        public string MarkElementId { get; }
        public string IncomingElementId { get; }
        public string AxisSubtype { get; }
        public bool HasValue => !string.IsNullOrEmpty(MarkElementId) && !string.IsNullOrEmpty(IncomingElementId);

        public override string ToString()
        {
            if (string.IsNullOrEmpty(AxisSubtype))
            {
                return $"{MarkElementId}->{IncomingElementId}";
            }

            return $"{MarkElementId}->{IncomingElementId}/{AxisSubtype}";
        }

        public bool Equals(ReactionKey other)
        {
            return string.Equals(MarkElementId, other.MarkElementId, StringComparison.Ordinal) &&
                   string.Equals(IncomingElementId, other.IncomingElementId, StringComparison.Ordinal) &&
                   string.Equals(AxisSubtype, other.AxisSubtype, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is ReactionKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + MarkElementId.GetHashCode();
                hash = hash * 23 + IncomingElementId.GetHashCode();
                hash = hash * 23 + AxisSubtype.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(ReactionKey left, ReactionKey right) => left.Equals(right);
        public static bool operator !=(ReactionKey left, ReactionKey right) => !left.Equals(right);

        public static ReactionKey From(MarkSlot slot, MarkElementView incoming, string axisSubtype)
        {
            string markElement = slot.Definition != null ? slot.Definition.elementId : string.Empty;
            string incomingElement = incoming.ElementId ?? string.Empty;
            return new ReactionKey(markElement, incomingElement, axisSubtype);
        }

        public static readonly ReactionKey None = new ReactionKey(string.Empty, string.Empty, string.Empty);
    }

    public readonly struct MarkReactionContext
    {
        public MarkReactionContext(CombatantState attacker, CombatantState target, ReactionKey reactionKey, int executionId)
        {
            Attacker = attacker;
            Target = target;
            ReactionKey = reactionKey;
            ExecutionId = executionId;
        }

        public CombatantState Attacker { get; }
        public CombatantState Target { get; }
        public ReactionKey ReactionKey { get; }
        public int ExecutionId { get; }
    }

    public interface IMarkReactionResolver
    {
        /// <summary>
        /// Returns true if a reaction id was found for the given key.
        /// </summary>
        bool TryResolveId(ReactionKey key, out string reactionId);

        /// <summary>
        /// Executes the reaction side-effects given a valid reaction id.
        /// </summary>
        void Execute(MarkReactionContext context, string reactionId);
    }

    /// <summary>
    /// Resolver por defecto que no produce ni ejecuta reacciones.
    /// </summary>
    public sealed class NoOpMarkReactionResolver : IMarkReactionResolver
    {
        public bool TryResolveId(ReactionKey key, out string reactionId)
        {
            reactionId = null;
            return false;
        }

        public void Execute(MarkReactionContext context, string reactionId)
        {
            // No-op
        }
    }
}
