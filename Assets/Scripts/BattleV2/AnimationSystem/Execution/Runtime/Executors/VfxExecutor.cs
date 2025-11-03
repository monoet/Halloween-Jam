using System.Text;
using System.Threading.Tasks;
using BattleV2.Actions;
using BattleV2.AnimationSystem.Execution.Routers;
using BattleV2.Core;

namespace BattleV2.AnimationSystem.Execution.Runtime.Executors
{
    /// <summary>
    /// Routes VFX requests through the router bundle.
    /// </summary>
    public sealed class VfxExecutor : IActionStepExecutor
    {
        public const string ExecutorId = "vfx";
        private const string LogScope = "AnimStep/Vfx";

        public string Id => ExecutorId;

        public bool CanExecute(ActionStep step)
        {
            return step.HasBinding;
        }

        public Task ExecuteAsync(StepExecutionContext context)
        {
            if (!context.Step.HasBinding)
            {
                BattleLogger.Warn(LogScope, $"Step '{context.Step.Id ?? "(no id)"}' missing VFX binding id.");
                return Task.CompletedTask;
            }

            var bundle = context.RouterBundle;
            if (bundle == null || bundle.VfxService == null)
            {
                BattleLogger.Warn(LogScope, "No VFX service configured. Skipping playback.");
                return Task.CompletedTask;
            }

            var rawPayload = BuildPayloadString(context.Step.BindingId, context.Step.Parameters);
            var payload = AnimationEventPayload.Parse(rawPayload);

            var action = context.Request.Selection.Action;
            var impactEvent = new AnimationImpactEvent(
                context.Actor,
                target: null,
                action: action,
                impactIndex: 0,
                impactCount: 1,
                tag: context.Step.Id ?? context.Step.BindingId,
                payload: rawPayload);

            bool played = bundle.VfxService.TryPlay(context.Step.BindingId, impactEvent, payload);
            if (!played)
            {
                BattleLogger.Warn(LogScope, $"Unable to play VFX '{context.Step.BindingId}' for actor '{context.Actor?.name ?? "(null)"}'.");
            }

            return Task.CompletedTask;
        }

        private static string BuildPayloadString(string bindingId, ActionStepParameters parameters)
        {
            if (parameters.IsEmpty)
            {
                return $"id={bindingId}";
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

            return builder.ToString();
        }
    }
}
