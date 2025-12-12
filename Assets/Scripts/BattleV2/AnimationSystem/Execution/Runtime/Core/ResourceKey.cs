using System;

namespace BattleV2.AnimationSystem.Execution.Runtime.Core
{
    /// <summary>
    /// Identifies an exclusive execution resource (e.g. Locomotion for an actor/binding).
    /// Used to prevent overlaps by locking at a finer granularity than executorId.
    /// </summary>
    public readonly struct ResourceKey : IEquatable<ResourceKey>
    {
        public ResourceKey(string channel, int actorId, int bindingId = 0)
        {
            Channel = channel ?? string.Empty;
            ActorId = actorId;
            BindingId = bindingId;
        }

        public string Channel { get; }
        public int ActorId { get; }
        public int BindingId { get; }

        public bool Equals(ResourceKey other)
        {
            return string.Equals(Channel, other.Channel, StringComparison.OrdinalIgnoreCase)
                && ActorId == other.ActorId
                && BindingId == other.BindingId;
        }

        public override bool Equals(object obj) => obj is ResourceKey other && Equals(other);

        public override int GetHashCode()
        {
            return HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(Channel ?? string.Empty),
                ActorId,
                BindingId);
        }

        public override string ToString() => $"{Channel}(actor={ActorId},binding={BindingId})";
    }
}

