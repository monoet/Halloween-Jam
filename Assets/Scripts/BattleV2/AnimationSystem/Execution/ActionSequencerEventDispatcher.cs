using BattleV2.Core;

namespace BattleV2.AnimationSystem.Execution
{
    internal sealed class ActionSequencerEventDispatcher
    {
        private readonly AnimationRequest request;
        private readonly IAnimationEventBus eventBus;

        public ActionSequencerEventDispatcher(AnimationRequest request, IAnimationEventBus eventBus)
        {
            this.request = request;
            this.eventBus = eventBus;
        }

        public void PublishPhaseEvent(in SequencerScheduledEvent scheduled)
        {
            var evt = new AnimationPhaseEvent(
                request.Actor,
                request.Selection,
                scheduled.Index + 1,
                scheduled.TotalCount,
                scheduled.Payload);
            eventBus?.Publish(evt);
        }

        public void PublishImpactEvent(in SequencerScheduledEvent scheduled)
        {
            CombatantState target = null;
            if (request.Targets != null && request.Targets.Count > 0)
            {
                int clamped = System.Math.Min(scheduled.Index, request.Targets.Count - 1);
                target = request.Targets[clamped];
            }

            var evt = new AnimationImpactEvent(
                request.Actor,
                target,
                request.Selection.Action,
                scheduled.Index + 1,
                scheduled.TotalCount,
                scheduled.Tag,
                scheduled.Payload);

            eventBus?.Publish(evt);
        }

        public void PublishWindowEvent(in SequencerScheduledEvent scheduled, bool isOpening)
        {
            var evt = new AnimationWindowEvent(
                request.Actor,
                scheduled.Tag,
                scheduled.Phase.StartNormalized,
                scheduled.Phase.EndNormalized,
                isOpening,
                scheduled.Index + 1,
                scheduled.TotalCount);
            eventBus?.Publish(evt);
        }

        public void PublishLockEvent(bool locked, string reason)
        {
            var evt = new AnimationLockEvent(request.Actor, locked, reason);
            eventBus?.Publish(evt);
        }
    }
}

