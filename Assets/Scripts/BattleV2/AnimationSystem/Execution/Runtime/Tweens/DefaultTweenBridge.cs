using System;
using System.Threading;
using System.Threading.Tasks;
using BattleV2.Core;
using BattleV2.Orchestration.Runtime;
using UnityEngine;

namespace BattleV2.AnimationSystem.Execution.Runtime.Tweens
{
    /// <summary>
    /// Fallback tween bridge that interpolates transforms using the built-in TransformTween struct.
    /// </summary>
    public sealed class DefaultTweenBridge : ITweenBridge
    {
        private readonly IMainThreadInvoker invoker;

        public DefaultTweenBridge(IMainThreadInvoker invoker)
        {
            this.invoker = invoker ?? throw new ArgumentNullException(nameof(invoker));
        }

        public Task PlayAsync(TweenExecutionRequest request, CancellationToken token)
        {
            if (request.TargetTransform == null || !request.Tween.IsValid)
            {
                return Task.CompletedTask;
            }

            return invoker.RunAsync(() => PlayInternalAsync(request, token));
        }

        private async Task PlayInternalAsync(TweenExecutionRequest request, CancellationToken token)
        {
            var target = request.TargetTransform;
            var tween = request.Tween;

            Vector3 startPos = target.localPosition;
            Quaternion startRot = target.localRotation;
            Vector3 startScale = target.localScale;

            var curve = tween.Easing ?? AnimationCurve.Linear(0f, 0f, 1f, 1f);
            float elapsed = 0f;
            BattleLogger.Log("TweenBridge",
                $"START {target.name}: startPos={startPos} targetPos={tween.TargetLocalPosition} dur={tween.Duration}");

            while (elapsed < tween.Duration)
            {
                token.ThrowIfCancellationRequested();
                await invoker.NextFrameAsync(token);
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / tween.Duration);
                float eased = curve.Evaluate(t);

                if (tween.TargetLocalPosition.HasValue)
                {
                    target.localPosition = Vector3.LerpUnclamped(startPos, tween.TargetLocalPosition.Value, eased);
                }

                if (tween.TargetLocalRotation.HasValue)
                {
                    target.localRotation = Quaternion.SlerpUnclamped(startRot, tween.TargetLocalRotation.Value, eased);
                }

                if (tween.TargetLocalScale.HasValue)
                {
                    target.localScale = Vector3.LerpUnclamped(startScale, tween.TargetLocalScale.Value, eased);
                }

            }

            // Snap to final values to avoid floating-point drift.
            if (tween.TargetLocalPosition.HasValue)
            {
                target.localPosition = tween.TargetLocalPosition.Value;
            }

            if (tween.TargetLocalRotation.HasValue)
            {
                target.localRotation = tween.TargetLocalRotation.Value;
            }

            if (tween.TargetLocalScale.HasValue)
            {
                target.localScale = tween.TargetLocalScale.Value;
            }

            BattleLogger.Log("TweenBridge",
                $"END {target.name}: finalPos={target.localPosition} dur={tween.Duration}");
        }
    }
}
