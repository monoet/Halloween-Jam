using System;
using System.Collections.Generic;
using BattleV2.AnimationSystem;
using BattleV2.AnimationSystem.Execution.Routers;
using BattleV2.AnimationSystem.Execution.Runtime.CombatEvents;
using BattleV2.AnimationSystem.Runtime;
using UnityEngine;

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

            var actorName = evt.Actor != null ? evt.Actor.name : "(null)";
            Debug.Log($"THS00 [TimedHitService] window {(evt.IsOpening ? "open" : "close")} actor={actorName} tag={evt.Tag} idx={evt.WindowIndex}/{evt.WindowCount}", evt.Actor);

            clock.Sample();
            double timestamp = clock.Now;

            var list = GetOrCreate(evt.Actor);
            if (evt.IsOpening)
            {
                list.Add(new ActiveWindow(
                    evt.Tag,
                    evt.Payload,
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
            string toleranceId = !string.IsNullOrWhiteSpace(window.ToleranceId) ? window.ToleranceId : tag;
            var tolerance = toleranceProfile.Resolve(toleranceId);
            int resolvedIndex = window.WindowIndex > 0 ? window.WindowIndex : windowIndex;
            int resolvedCount = window.WindowCount > 0 ? window.WindowCount : windowCount;
            string resolvedTag = string.IsNullOrWhiteSpace(tag) ? window.Tag : tag;
            double centerTimestamp = window.ComputeCenterTimestamp(openTimestamp, closeTimestamp);

            if (inputBuffer.TryConsume(actor, openTimestamp, closeTimestamp, tolerance, out var buffered, out var deltaSeconds, centerTimestamp))
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

            var actorName = actor != null ? actor.name : "(null)";
            Debug.Log($"THS01 [TimedHitService] actor={actorName} tag={tag} judgment={judgment} delta={deltaMs:0.#}ms win={windowIndex}/{windowCount}", actor);

            eventBus.Publish(evt);

            EmitCombatFlag(actor, judgment);
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

        private static void EmitCombatFlag(CombatantState actor, TimedHitJudgment judgment)
        {
            if (actor == null)
            {
                return;
            }

            var installer = AnimationSystemInstaller.Current;
            var dispatcher = installer?.CombatEvents;
            if (dispatcher == null)
            {
                return;
            }

            string flag = judgment switch
            {
                TimedHitJudgment.Perfect => CombatEventFlags.Success,
                TimedHitJudgment.Good => CombatEventFlags.Impact,
                _ => CombatEventFlags.Missed
            };

            var actorName = actor != null ? actor.name : "(null)";
            Debug.Log($"THS02 [TimedHitService] emit flag='{flag}' actor={actorName}", actor);
            dispatcher.EmitExternalFlag(flag, actor);
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
            public ActiveWindow(
                string tag,
                string payload,
                double openTimestamp,
                float startNormalized,
                float endNormalized,
                int windowIndex,
                int windowCount)
            {
                Tag = tag;
                Payload = AnimationEventPayload.Parse(payload);
                OpenTimestamp = openTimestamp;
                StartNormalized = startNormalized;
                EndNormalized = endNormalized;
                WindowIndex = windowIndex;
                WindowCount = windowCount;
                ToleranceId = ResolveToleranceId(tag, Payload);
                PerfectNormalized = ResolvePerfectNormalized(Payload);
            }

            public string Tag { get; }
            public AnimationEventPayload Payload { get; }
            public string ToleranceId { get; }
            public double PerfectNormalized { get; }
            public double OpenTimestamp { get; }
            public float StartNormalized { get; }
            public float EndNormalized { get; }
            public int WindowIndex { get; }
            public int WindowCount { get; }

            public double ComputeCenterTimestamp(double openTimestamp, double closeTimestamp)
            {
                double duration = closeTimestamp - openTimestamp;
                if (duration <= 0d)
                {
                    return openTimestamp;
                }

                if (!double.IsNaN(PerfectNormalized))
                {
                    double start = StartNormalized;
                    double end = EndNormalized;
                    double span = end - start;
                    if (span > 1e-6d)
                    {
                        double clamped = Clamp(PerfectNormalized, start, end);
                        double fraction = Clamp01((clamped - start) / span);
                        return openTimestamp + (duration * fraction);
                    }
                }

                return openTimestamp + (duration * 0.5d);
            }

            private static string ResolveToleranceId(string tag, AnimationEventPayload payload)
            {
                if ((payload.TryGetString("toleranceProfileId", out var value) ||
                     payload.TryGetString("toleranceProfile", out value) ||
                     payload.TryGetString("toleranceId", out value) ||
                     payload.TryGetString("tolerance", out value)) &&
                    !string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }

                return tag;
            }

            private static double ResolvePerfectNormalized(AnimationEventPayload payload)
            {
                if (payload.TryGetDouble("perfect", out var perfect))
                {
                    return perfect;
                }

                if (payload.TryGetDouble("center", out var center))
                {
                    return center;
                }

                return double.NaN;
            }

            private static double Clamp01(double value)
            {
                if (value < 0d)
                {
                    return 0d;
                }

                if (value > 1d)
                {
                    return 1d;
                }

                return value;
            }

            private static double Clamp(double value, double min, double max)
            {
                if (value < min)
                {
                    return min;
                }

                if (value > max)
                {
                    return max;
                }

                return value;
            }
        }
    }
}
