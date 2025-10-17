using System;

namespace HalloweenJam.Combat.Animations
{
    /// <summary>
    /// Represents the coarse phases of an attack animation.
    /// </summary>
    public enum AttackAnimationPhase
    {
        Charge,
        Lunge,
        Impact,
        Recover
    }

    /// <summary>
    /// Optional interface for animators that expose phase events.
    /// </summary>
    public interface IAttackAnimationPhases
    {
        event Action<AttackAnimationPhase> PhaseChanged;
    }
}
