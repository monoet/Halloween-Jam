using System.Text;
using System.Threading.Tasks;
using BattleV2.AnimationSystem.Execution.Routers;
using BattleV2.Core;

namespace BattleV2.AnimationSystem.Execution.Runtime.Executors
{
    /// <summary>
    /// Routes an SFX request through the animation router bundle.
    /// </summary>
    public sealed class SfxExecutor : IActionStepExecutor
    {
        public const string ExecutorId = "sfx";
        private const string LogScope = "AnimStep/Sfx";

        public string Id => ExecutorId;

        public bool CanExecute(ActionStep step)
        {
            return step.HasBinding;
        }

        public Task ExecuteAsync(StepExecutionContext context)
        {
            if (!context.Step.HasBinding)
            {
                BattleLogger.Warn(LogScope, $"Step '{context.Step.Id ?? "(no id)"}' missing SFX binding id.");
                return Task.CompletedTask;
            }

            var bundle = context.RouterBundle;
            if (bundle == null || bundle.SfxService == null)
            {
                BattleLogger.Warn(LogScope, "No SFX service configured. Skipping playback.");
                return Task.CompletedTask;
            }

            var payload = BuildPayload(context.Step.BindingId, context.Step.Parameters);
            bool played = bundle.SfxService.TryPlay(
                context.Step.BindingId,
                context.Actor,
                impactEvent: null,
                phaseEvent: null,
                payload);

            if (!played)
            {
                BattleLogger.Warn(LogScope, $"Unable to play SFX '{context.Step.BindingId}' for actor '{context.Actor?.name ?? "(null)"}'.");
            }

            return Task.CompletedTask;
        }

        private static AnimationEventPayload BuildPayload(string bindingId, ActionStepParameters parameters)
        {
            if (parameters.IsEmpty)
            {
                return AnimationEventPayload.Parse($"id={bindingId}");
            }

            var builder = new StringBuilder();
            builder.Append("id=").Append(bindingId);

            foreach (var kvp in parameters.Data)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key) || string.IsNullOrWhiteSpace(kvp.Value))
                {
                    continue;
                }

                builder.Append(';').Append(kvp.Key).Append('=').Append(kvp.Value);
            }

            return AnimationEventPayload.Parse(builder.ToString());
        }
    }
}
