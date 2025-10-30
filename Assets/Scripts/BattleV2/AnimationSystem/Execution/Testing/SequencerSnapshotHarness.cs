using System;
using System.Collections.Generic;
using BattleV2.AnimationSystem.Timelines;
using BattleV2.Core;

namespace BattleV2.AnimationSystem.Execution.Testing
{
    public sealed class SequencerSnapshot
    {
        public SequencerSnapshot(int framesPerSecond, IReadOnlyList<SequencerEventInfo> events)
        {
            FramesPerSecond = framesPerSecond;
            Events = events;
        }

        public int FramesPerSecond { get; }
        public IReadOnlyList<SequencerEventInfo> Events { get; }
    }

    public static class SequencerSnapshotHarness
    {
        public static IReadOnlyList<SequencerSnapshot> CaptureSnapshots(
            ActionTimeline timeline,
            ITimelineCompiler compiler,
            params int[] fps)
        {
            if (timeline == null)
            {
                throw new ArgumentNullException(nameof(timeline));
            }

            if (compiler == null)
            {
                throw new ArgumentNullException(nameof(compiler));
            }

            if (fps == null || fps.Length == 0)
            {
                fps = new[] { 60 };
            }

            float duration = timeline.Info.Length;
            if (duration <= 0f)
            {
                duration = 1f;
            }

            var compiled = compiler.Compile(timeline, duration);
            var results = new List<SequencerSnapshot>(fps.Length);

            foreach (int framesPerSecond in fps)
            {
                if (framesPerSecond <= 0)
                {
                    continue;
                }

                var snapshot = CaptureSingle(compiled, framesPerSecond);
                results.Add(snapshot);
            }

            return results;
        }

        private static SequencerSnapshot CaptureSingle(CompiledTimeline compiled, int fps)
        {
            var clock = new ManualCombatClock();
            var eventBus = new AnimationEventBus();
            var lockManager = new ActionLockManager();

            var request = new AnimationRequest(null, default, Array.Empty<CombatantState>(), 1f);
            var sequencer = new ActionSequencer(request, compiled, clock, eventBus, lockManager);

            var events = new List<SequencerEventInfo>();
            sequencer.EventDispatched += info => events.Add(info);

            sequencer.Start();

            double step = 1.0d / fps;
            double safety = compiled.Duration + 2.0d;
            double elapsed = 0d;

            while (!sequencer.IsCompleted && elapsed < safety)
            {
                elapsed += step;
                clock.Advance(step);
                sequencer.Tick();
            }

            return new SequencerSnapshot(fps, events);
        }

        private sealed class ManualCombatClock : ICombatClock
        {
            private double now;

            public double Now => now;

            public void Reset()
            {
                now = 0d;
            }

            public void Sample()
            {
            }

            public void Advance(double deltaSeconds)
            {
                now = Math.Max(0d, now + Math.Max(0d, deltaSeconds));
            }
        }
    }
}

