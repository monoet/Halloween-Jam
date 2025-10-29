using System;
using System.Collections.Generic;
using BattleV2.AnimationSystem;

namespace BattleV2.AnimationSystem.Execution.Runtime
{
    public interface ITimedHitService : IDisposable
    {
        void RegisterInput(CombatantState actor, string source = null, double? timestamp = null);
        void Reset(CombatantState actor);
        void ResetAll();
    }

    public sealed class TimedHitService : ITimedHitService
    {
        private readonly ICombatClock clock;
        private readonly ITimedInputBuffer inputBuffer;
        private readonly ITimedHitToleranceProfile toleranceProfile;
        private readonly IAnimationEventBus eventBus;
        private readonly Dictionary<CombatantState, List<ActiveWindow>> activeWindows = new();
        private readonly List<IDisposable> subscriptions = new();
        private bool disposed;

        public TimedHitService(
            ICombatClock clock,
            ITimedInputBuffer inputBuffer,
            ITimedHitToleranceProfile toleranceProfile,
            IAnimationEventBus eventBus)
        {
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.inputBuffer = inputBuffer ?? throw new ArgumentNullException(nameof(inputBuffer));
            this.toleranceProfile = toleranceProfile ?? throw new ArgumentNullException(nameof(toleranceProfile));
            this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));

            subscriptions.Add(eventBus.Subscribe<AnimationWindowEvent>(OnWindowEvent));
            subscriptions.Add(eventBus.Subscribe<AnimationLockEvent>(OnLockEvent));
        }

        public void RegisterInput(CombatantState actor, string source = null, double? timestamp = null)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(TimedHitService));
            }

            inputBuffer.Register(actor, source, timestamp);
        }

        public void Reset(CombatantState actor)
        {
            if (actor == null)
            {
                return;
            }

            activeWindows.Remove(actor);
            inputBuffer.Clear(actor);
        }

        public void ResetAll()
        {
            activeWindows.Clear();
            inputBuffer.ClearAll();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            for (int i = 0; i < subscriptions.Count; i++)
            {
                subscriptions[i]?.Dispose();
            }

            subscriptions.Clear();
            activeWindows.Clear();
        }

        private void OnWindowEvent(AnimationWindowEvent evt)
        {
            if (disposed || evt.Actor == null)
            {
                return;
            }

            clock.Sample();
            double timestamp = clock.Now;

            var list = GetOrCreate(evt.Actor);
            if (evt.IsOpening)
            {
                list.Add(new ActiveWindow(
                    evt.Tag,
                    timestamp,
                    evt.WindowStart,
                    evt.WindowEnd,
                    evt.WindowIndex,
                    evt.WindowCount));
            }
            else
            {
                ResolveWindow(evt.Actor, evt.Tag, evt.WindowIndex, evt.WindowCount, list, timestamp);
            }
        }

        private void ResolveWindow(
            CombatantState actor,
            string tag,
            int windowIndex,
            int windowCount,
            List<ActiveWindow> list,
            double closeTimestamp)
        {
            if (list.Count == 0)
            {
                PublishMiss(actor, tag, windowIndex, windowCount, double.NaN, double.NaN, closeTimestamp, closeTimestamp, consumedInput: false);
                return;
            }

            int matchIndex = FindWindowIndex(list, tag, windowIndex);
            if (matchIndex < 0)
            {
                matchIndex = 0;
            }

            var window = list[matchIndex];
            list.RemoveAt(matchIndex);

            double openTimestamp = window.OpenTimestamp;
            var tolerance = toleranceProfile.Resolve(tag);
            int resolvedIndex = window.WindowIndex > 0 ? window.WindowIndex : windowIndex;
            int resolvedCount = window.WindowCount > 0 ? window.WindowCount : windowCount;
            string resolvedTag = string.IsNullOrWhiteSpace(tag) ? window.Tag : tag;

            if (inputBuffer.TryConsume(actor, openTimestamp, closeTimestamp, tolerance, out var buffered, out var deltaSeconds))
            {
                double deltaMs = Math.Abs(deltaSeconds) * 1000d;
                var judgment = Classify(deltaMs, tolerance);
                PublishResult(actor, resolvedTag, resolvedIndex, resolvedCount, deltaMs, buffered.Timestamp, openTimestamp, closeTimestamp, judgment, consumedInput: true);
            }
            else
            {
                PublishMiss(actor, resolvedTag, resolvedIndex, resolvedCount, double.PositiveInfinity, double.NaN, openTimestamp, closeTimestamp, consumedInput: false);
            }
        }

        private static int FindWindowIndex(List<ActiveWindow> list, string tag, int windowIndex)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var candidate = list[i];
                if (windowIndex > 0 && candidate.WindowIndex == windowIndex)
                {
                    return i;
                }

                if (!string.IsNullOrWhiteSpace(tag) && string.Equals(candidate.Tag, tag, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return windowIndex <= 0 && string.IsNullOrWhiteSpace(tag) ? 0 : -1;
        }

        private void PublishResult(
            CombatantState actor,
            string tag,
            int windowIndex,
            int windowCount,
            double deltaMs,
            double inputTimestamp,
            double windowOpenedAt,
            double windowClosedAt,
            TimedHitJudgment judgment,
            bool consumedInput)
        {
            var evt = new TimedHitResultEvent(
                actor,
                tag,
                judgment,
                deltaMs,
                inputTimestamp,
                windowIndex,
                windowCount,
                consumedInput,
                windowOpenedAt,
                windowClosedAt);
            eventBus.Publish(evt);
        }

        private void PublishMiss(
            CombatantState actor,
            string tag,
            int windowIndex,
            int windowCount,
            double deltaMs,
            double inputTimestamp,
            double windowOpenedAt,
            double windowClosedAt,
            bool consumedInput)
        {
            PublishResult(
                actor,
                tag,
                windowIndex,
                windowCount,
                deltaMs,
                inputTimestamp,
                windowOpenedAt,
                windowClosedAt,
                TimedHitJudgment.Miss,
                consumedInput);
        }

        private static TimedHitJudgment Classify(double deltaMs, TimedHitTolerance tolerance)
        {
            if (deltaMs <= tolerance.PerfectMilliseconds)
            {
                return TimedHitJudgment.Perfect;
            }

            if (deltaMs <= tolerance.GoodMilliseconds)
            {
                return TimedHitJudgment.Good;
            }

            return TimedHitJudgment.Miss;
        }

        private void OnLockEvent(AnimationLockEvent evt)
        {
            if (evt.Actor == null || evt.IsLocked)
            {
                return;
            }

            Reset(evt.Actor);
        }

        private List<ActiveWindow> GetOrCreate(CombatantState actor)
        {
            if (!activeWindows.TryGetValue(actor, out var list))
            {
                list = new List<ActiveWindow>(4);
                activeWindows[actor] = list;
            }

            return list;
        }

        private readonly struct ActiveWindow
        {
            public ActiveWindow(string tag, double openTimestamp, float startNormalized, float endNormalized, int windowIndex, int windowCount)
            {
                Tag = tag;
                OpenTimestamp = openTimestamp;
                StartNormalized = startNormalized;
                EndNormalized = endNormalized;
                WindowIndex = windowIndex;
                WindowCount = windowCount;
            }

            public string Tag { get; }
            public double OpenTimestamp { get; }
            public float StartNormalized { get; }
            public float EndNormalized { get; }
            public int WindowIndex { get; }
            public int WindowCount { get; }
        }
    }
}
