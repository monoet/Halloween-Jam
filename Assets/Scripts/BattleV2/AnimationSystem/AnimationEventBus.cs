using System;
using System.Collections.Generic;
using BattleV2.Actions;
using BattleV2.Core;
using BattleV2.Providers;

namespace BattleV2.AnimationSystem
{
    public interface IAnimationEventBus
    {
        void Publish<TEvent>(TEvent evt) where TEvent : struct;
        IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct;
    }

    /// <summary>
    /// Lightweight synchronous pub/sub tailored for the JRPG animation pipeline.
    /// </summary>
    public sealed class AnimationEventBus : IAnimationEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> subscribers = new();

        public void Publish<TEvent>(TEvent evt) where TEvent : struct
        {
            var type = typeof(TEvent);
            if (!subscribers.TryGetValue(type, out var handlers))
            {
                return;
            }

            for (int i = 0; i < handlers.Count; i++)
            {
                if (handlers[i] is Action<TEvent> action)
                {
                    action(evt);
                }
            }
        }

        public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : struct
        {
            if (handler == null)
            {
                return EmptyDisposable.Instance;
            }

            var type = typeof(TEvent);
            if (!subscribers.TryGetValue(type, out var handlers))
            {
                handlers = new List<Delegate>();
                subscribers[type] = handlers;
            }

            handlers.Add(handler);

            return new Subscription(() =>
            {
                if (!subscribers.TryGetValue(type, out var list))
                {
                    return;
                }

                list.Remove(handler);
                if (list.Count == 0)
                {
                    subscribers.Remove(type);
                }
            });
        }

        private sealed class Subscription : IDisposable
        {
            private Action dispose;

            public Subscription(Action disposal)
            {
                dispose = disposal;
            }

            public void Dispose()
            {
                dispose?.Invoke();
                dispose = null;
            }
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance = new();
            public void Dispose() { }
        }
    }

    #region Events

    public readonly struct AnimationPhaseEvent
    {
        public AnimationPhaseEvent(CombatantState actor, BattleSelection selection, int phaseIndex, int phaseCount, string payload)
        {
            Actor = actor;
            Selection = selection;
            PhaseIndex = phaseIndex;
            PhaseCount = phaseCount;
            Payload = payload;
        }

        public CombatantState Actor { get; }
        public BattleSelection Selection { get; }
        public int PhaseIndex { get; }
        public int PhaseCount { get; }
        public string Payload { get; }
    }

    public readonly struct AnimationImpactEvent
    {
        public AnimationImpactEvent(
            CombatantState actor,
            CombatantState target,
            BattleActionData action,
            int impactIndex,
            int impactCount,
            string tag,
            string payload)
        {
            Actor = actor;
            Target = target;
            Action = action;
            ImpactIndex = impactIndex;
            ImpactCount = impactCount;
            Tag = tag;
            Payload = payload;
        }

        public CombatantState Actor { get; }
        public CombatantState Target { get; }
        public BattleActionData Action { get; }
        public int ImpactIndex { get; }
        public int ImpactCount { get; }
        public string Tag { get; }
        public string Payload { get; }
    }

    public readonly struct AnimationWindowEvent
    {
        public AnimationWindowEvent(
            CombatantState actor,
            string tag,
            string payload,
            float windowStart,
            float windowEnd,
            bool isOpening,
            int windowIndex,
            int windowCount)
        {
            Actor = actor;
            Tag = tag;
            Payload = payload;
            WindowStart = windowStart;
            WindowEnd = windowEnd;
            IsOpening = isOpening;
            WindowIndex = windowIndex;
            WindowCount = windowCount;
        }

        public CombatantState Actor { get; }
        public string Tag { get; }
        public string Payload { get; }
        public float WindowStart { get; }
        public float WindowEnd { get; }
        public bool IsOpening { get; }
        public int WindowIndex { get; }
        public int WindowCount { get; }
    }

    public readonly struct AnimationLockEvent
    {
        public AnimationLockEvent(CombatantState actor, bool isLocked, string reason)
        {
            Actor = actor;
            IsLocked = isLocked;
            Reason = reason;
        }

        public CombatantState Actor { get; }
        public bool IsLocked { get; }
        public string Reason { get; }
    }

    public enum TimedHitJudgment
    {
        Perfect,
        Good,
        Miss
    }

    public readonly struct TimedHitResultEvent
    {
        public TimedHitResultEvent(
            CombatantState actor,
            string tag,
            TimedHitJudgment judgment,
            double deltaMilliseconds,
            double inputTimestamp,
            int windowIndex,
            int windowCount,
            bool consumedInput,
            double windowOpenedAt,
            double windowClosedAt)
        {
            Actor = actor;
            Tag = tag;
            Judgment = judgment;
            DeltaMilliseconds = deltaMilliseconds;
            InputTimestamp = inputTimestamp;
            WindowIndex = windowIndex;
            WindowCount = windowCount;
            ConsumedInput = consumedInput;
            WindowOpenedAt = windowOpenedAt;
            WindowClosedAt = windowClosedAt;
        }

        public CombatantState Actor { get; }
        public string Tag { get; }
        public TimedHitJudgment Judgment { get; }
        public double DeltaMilliseconds { get; }
        public double InputTimestamp { get; }
        public int WindowIndex { get; }
        public int WindowCount { get; }
        public bool ConsumedInput { get; }
        public double WindowOpenedAt { get; }
        public double WindowClosedAt { get; }
    }

    #endregion
}
