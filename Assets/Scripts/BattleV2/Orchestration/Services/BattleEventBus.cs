using System;
using System.Collections.Generic;
using BattleV2.AnimationSystem;
using BattleV2.Core;
using BattleV2.Orchestration.Events;

namespace BattleV2.Orchestration.Services
{
    public interface IBattleEventBus
    {
        void Publish<TEvent>(TEvent evt);
        IDisposable Subscribe<TEvent>(Action<TEvent> handler);
    }

    /// <summary>
    /// Simplest synchronous pub/sub. Se reemplazará con versión robusta más adelante.
    /// </summary>
    public sealed class BattleEventBus : IBattleEventBus, IAnimationEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> subscribers = new();

        public void Publish<TEvent>(TEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            if (evt is ActionStartedEvent started)
            {
                var actor = started.Actor;
                var selection = started.Selection;
                var targets = started.Targets ?? Array.Empty<CombatantState>();
                string actorLabel = actor != null ? $"{actor.DisplayName}#{actor.GetInstanceID()}" : "(null)";
                string actionId = selection.Action != null ? selection.Action.id : "(null)";
                string scope = selection.Action != null ? selection.Action.targetShape.ToString() : "(unknown)";

                BattleDiagnostics.Log(
                    "ActionStartedEvent",
                    $"actor={actorLabel} side={(actor != null && actor.IsPlayer ? "Player" : "Enemy")} actionId={actionId} scope={scope} targets={targets.Count}",
                    actor);
            }

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

        public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        {
            if (handler == null)
            {
                return Disposable.Empty;
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

        void IAnimationEventBus.Publish<TEvent>(TEvent evt)
        {
            Publish(evt);
        }

        IDisposable IAnimationEventBus.Subscribe<TEvent>(Action<TEvent> handler)
        {
            return Subscribe(handler);
        }

        private sealed class Subscription : IDisposable
        {
            private Action dispose;

            public Subscription(Action disposeAction)
            {
                dispose = disposeAction;
            }

            public void Dispose()
            {
                dispose?.Invoke();
                dispose = null;
            }
        }

        private sealed class Disposable : IDisposable
        {
            public static readonly Disposable Empty = new();
            public void Dispose() { }
        }
    }
}
