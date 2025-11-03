using System;
using System.Threading.Tasks;
using BattleV2.Core;
using BattleV2.Orchestration.Runtime;
using UnityEngine;

namespace BattleV2.AnimationSystem.Execution.Runtime.Executors
{
    /// <summary>
    /// Plays an animator clip on the combatant wrapper using the clip id resolved from bindings.
    /// </summary>
    public sealed class AnimatorClipExecutor : IActionStepExecutor
    {
        public const string ExecutorId = "animatorClip";
        private const string LogScope = "AnimStep/Animator";

        public string Id => ExecutorId;

        public bool CanExecute(ActionStep step)
        {
            return step.HasBinding;
        }

        public async Task ExecuteAsync(StepExecutionContext context)
        {
            if (!context.Step.HasBinding)
            {
                BattleLogger.Warn(LogScope, $"Step '{context.Step.Id ?? "(no id)"}' missing clip binding id.");
                return;
            }

            if (!TryResolveClip(context, context.Step.BindingId, out var clip))
            {
                BattleLogger.Warn(LogScope, $"Clip '{context.Step.BindingId}' not found for actor '{context.Actor?.name ?? "(null)"}'.");
                return;
            }

            var parameters = context.Step.Parameters;
            var playbackRequest = BuildPlaybackRequest(clip, parameters);

            try
            {
                await context.Wrapper.PlayAsync(playbackRequest, context.CancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Propagate cancellation silently; scheduler handles token state.
            }
        }

        private static bool TryResolveClip(StepExecutionContext context, string clipId, out AnimationClip clip)
        {
            clip = null;
            if (string.IsNullOrWhiteSpace(clipId))
            {
                return false;
            }

            if (context.Bindings != null && context.Bindings.TryGetClip(clipId, out clip))
            {
                return true;
            }

            return context.Wrapper != null && context.Wrapper.TryGetClip(clipId, out clip);
        }

        private static AnimationPlaybackRequest BuildPlaybackRequest(AnimationClip clip, ActionStepParameters parameters)
        {
            float speed = 1f;
            bool loop = false;
            float normalizedStart = 0f;

            if (parameters.TryGetFloat("speed", out var speedValue))
            {
                speed = Mathf.Approximately(speedValue, 0f) ? 1f : speedValue;
            }

            if (parameters.TryGetBool("loop", out var loopValue))
            {
                loop = loopValue;
            }

            if (parameters.TryGetFloat("start", out var startNormalized))
            {
                normalizedStart = Mathf.Clamp01(startNormalized);
            }
            else if (parameters.TryGetFloat("startNormalized", out var altStart))
            {
                normalizedStart = Mathf.Clamp01(altStart);
            }

            return AnimationPlaybackRequest.ForAnimatorClip(clip, speed, normalizedStart, loop);
        }
    }
}
