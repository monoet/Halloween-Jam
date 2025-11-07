using System;
using System.Threading.Tasks;
using BattleV2.Common;
using BattleV2.Core;
using BattleV2.Orchestration.Runtime;
using UnityEngine;

namespace BattleV2.AnimationSystem.Execution.Runtime.Executors
{
    /// <summary>
    /// Applies a transform tween defined in bindings to the combatant wrapper.
    /// </summary>
    public sealed class TweenExecutor : IActionStepExecutor
    {
        public const string ExecutorId = "tween";
        private const string LogScope = "AnimStep/Tween";

        public string Id => ExecutorId;

        public bool CanExecute(ActionStep step)
        {
            return step.HasBinding;
        }

        public async Task ExecuteAsync(StepExecutionContext context)
        {
            await UnityMainThread.SwitchAsync().ConfigureAwait(false);

            if (!context.Step.HasBinding)
            {
                BattleLogger.Warn(LogScope, $"Step '{context.Step.Id ?? "(no id)"}' missing tween binding id.");
                return;
            }

            if (!TryResolveTween(context, context.Step.BindingId, out var tween))
            {
                BattleLogger.Warn(LogScope, $"Tween '{context.Step.BindingId}' not found in bindings or wrapper.");
                return;
            }

            if (!tween.IsValid)
            {
                BattleLogger.Warn(LogScope, $"Tween '{context.Step.BindingId}' is not valid (missing targets or duration).");
                return;
            }

            tween = ApplyOverrides(tween, context.Step.Parameters);

            var request = AnimationPlaybackRequest.ForTransformTween(tween);

            try
            {
                await context.Wrapper.PlayAsync(request, context.CancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Cancellation expected when the scheduler stops the step.
            }
        }

        private static bool TryResolveTween(StepExecutionContext context, string tweenId, out TransformTween tween)
        {
            tween = TransformTween.None;
            if (string.IsNullOrWhiteSpace(tweenId))
            {
                return false;
            }

            if (context.Bindings != null && context.Bindings.TryGetTween(tweenId, out tween))
            {
                return true;
            }

            return context.Wrapper != null && context.Wrapper.TryGetTween(tweenId, out tween);
        }

        private static TransformTween ApplyOverrides(TransformTween tween, ActionStepParameters parameters)
        {
            if (parameters.TryGetFloat("duration", out var duration) && duration > 0f)
            {
                tween.Duration = duration;
            }

            if (parameters.TryGetFloat("posX", out var posX) ||
                parameters.TryGetFloat("posY", out var posY) ||
                parameters.TryGetFloat("posZ", out var posZ))
            {
                var current = tween.TargetLocalPosition ?? Vector3.zero;
                float x = parameters.TryGetFloat("posX", out posX) ? posX : current.x;
                float y = parameters.TryGetFloat("posY", out posY) ? posY : current.y;
                float z = parameters.TryGetFloat("posZ", out posZ) ? posZ : current.z;
                tween.TargetLocalPosition = new Vector3(x, y, z);
            }

            if (parameters.TryGetFloat("scaleX", out var scaleX) ||
                parameters.TryGetFloat("scaleY", out var scaleY) ||
                parameters.TryGetFloat("scaleZ", out var scaleZ))
            {
                var current = tween.TargetLocalScale ?? Vector3.one;
                float x = parameters.TryGetFloat("scaleX", out scaleX) ? scaleX : current.x;
                float y = parameters.TryGetFloat("scaleY", out scaleY) ? scaleY : current.y;
                float z = parameters.TryGetFloat("scaleZ", out scaleZ) ? scaleZ : current.z;
                tween.TargetLocalScale = new Vector3(x, y, z);
            }

            if (parameters.TryGetFloat("rotX", out var rotX) ||
                parameters.TryGetFloat("rotY", out var rotY) ||
                parameters.TryGetFloat("rotZ", out var rotZ))
            {
                var current = tween.TargetLocalRotation ?? Quaternion.identity;
                float x = parameters.TryGetFloat("rotX", out rotX) ? rotX : current.eulerAngles.x;
                float y = parameters.TryGetFloat("rotY", out rotY) ? rotY : current.eulerAngles.y;
                float z = parameters.TryGetFloat("rotZ", out rotZ) ? rotZ : current.eulerAngles.z;
                tween.TargetLocalRotation = Quaternion.Euler(x, y, z);
            }

            return tween;
        }
    }
}
