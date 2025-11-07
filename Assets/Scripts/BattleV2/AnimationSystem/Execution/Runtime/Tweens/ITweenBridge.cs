using System.Threading;
using System.Threading.Tasks;
using BattleV2.Orchestration.Runtime;
using UnityEngine;

namespace BattleV2.AnimationSystem.Execution.Runtime.Tweens
{
    /// <summary>
    /// Executes a tween request on a specific transform and completes when the interpolation ends or cancels.
    /// </summary>
    public interface ITweenBridge
    {
        Task PlayAsync(TweenExecutionRequest request, CancellationToken token);
    }

    /// <summary>
    /// Normalized data passed to <see cref="ITweenBridge"/> so bridges do not depend on scheduler types.
    /// </summary>
    public readonly struct TweenExecutionRequest
    {
        public TweenExecutionRequest(
            Transform targetTransform,
            TransformTween tween)
        {
            TargetTransform = targetTransform;
            Tween = tween;
        }

        /// <summary>Transform affected by the tween. Must be part of the combatant actor hierarchy.</summary>
        public Transform TargetTransform { get; }

        /// <summary>Resolved tween payload (either literal data or output from a provider).</summary>
        public TransformTween Tween { get; }
    }
}
