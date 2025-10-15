using System;

namespace HalloweenJam.Combat.Animations
{
    /// <summary>
    /// Provides a common contract for playing combat attack animations.
    /// </summary>
    public interface IAttackAnimator
    {
        void PlayAttack(Action onImpact = null, Action onComplete = null);
    }
}

