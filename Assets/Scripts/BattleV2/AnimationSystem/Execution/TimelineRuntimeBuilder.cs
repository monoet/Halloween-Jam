using System;
using BattleV2.AnimationSystem.Timelines;
using UnityEngine;

namespace BattleV2.AnimationSystem.Execution
{
    public sealed class TimelineRuntimeBuilder
    {
        private readonly ITimelineCompiler compiler;
        private readonly ICombatClock clock;
        private readonly IAnimationEventBus eventBus;
        private readonly IActionLockManager lockManager;

        public TimelineRuntimeBuilder(
            ITimelineCompiler compiler,
            ICombatClock clock,
            IAnimationEventBus eventBus,
            IActionLockManager lockManager)
        {
            this.compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            this.lockManager = lockManager ?? throw new ArgumentNullException(nameof(lockManager));
        }

        public ActionSequencer Create(AnimationRequest request, ActionTimeline timeline)
        {
            if (timeline == null)
            {
                throw new ArgumentNullException(nameof(timeline));
            }

            float timelineDuration = ResolveDuration(timeline);
            var compiled = compiler.Compile(timeline, timelineDuration);
            return new ActionSequencer(request, compiled, clock, eventBus, lockManager);
        }

        private static float ResolveDuration(ActionTimeline timeline)
        {
            float duration = timeline.Info.Length;
            if (duration <= 0f)
            {
                duration = 1f;
            }

            return Mathf.Max(0.1f, duration);
        }
    }
}
