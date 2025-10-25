using BattleV2.Actions;
using BattleV2.Targeting;

namespace BattleV2.Orchestration.Services
{
    public interface ITriggeredEffectsService
    {
        void Enqueue(BattleActionData action, TargetSet? targets);
    }

    /// <summary>
    /// Stub de cola de efectos encadenados. Por ahora solo publica evento placeholder.
    /// </summary>
    public sealed class TriggeredEffectsService : ITriggeredEffectsService
    {
        private readonly IBattleEventBus eventBus;

        public TriggeredEffectsService(IBattleEventBus eventBus)
        {
            this.eventBus = eventBus;
        }

        public void Enqueue(BattleActionData action, TargetSet? targets)
        {
            if (action == null)
            {
                return;
            }

            // TODO: Integrar con ActionPipeline cuando exista implementación real.
            eventBus.Publish(new TriggeredEffectQueued(action, targets));
        }
    }

    public readonly struct TriggeredEffectQueued
    {
        public TriggeredEffectQueued(BattleActionData action, TargetSet? targets)
        {
            Action = action;
            Targets = targets;
        }

        public BattleActionData Action { get; }
        public TargetSet? Targets { get; }
    }
}
