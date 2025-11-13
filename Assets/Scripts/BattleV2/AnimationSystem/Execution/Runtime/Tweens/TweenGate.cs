using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace BattleV2.AnimationSystem.Execution.Runtime.Tweens
{
    /// <summary>
    /// Ensures only one tween runs per actor. New tweens kill the previous one automatically.
    /// </summary>
    public static class TweenGate
    {
        private static readonly Dictionary<int, Tween> ActiveTweens = new Dictionary<int, Tween>();
        private static readonly object Gate = new object();

        public static TweenHandle For(int actorId) => new TweenHandle(actorId);

        public static void KillAll(bool complete = false)
        {
            KeyValuePair<int, Tween>[] snapshot;
            lock (Gate)
            {
                snapshot = new KeyValuePair<int, Tween>[ActiveTweens.Count];
                int index = 0;
                foreach (var entry in ActiveTweens)
                {
                    snapshot[index++] = entry;
                }
                ActiveTweens.Clear();
            }

            for (int i = 0; i < snapshot.Length; i++)
            {
                var tween = snapshot[i].Value;
                if (tween == null)
                {
                    continue;
                }

                if (complete)
                {
                    tween.Complete();
                }
                else
                {
                    tween.Kill();
                }
            }
        }

        internal static void StartInternal(int actorId, Tween tween, bool killOnStart)
        {
            if (tween == null)
            {
                return;
            }

            Tween previous = null;
            bool replacingOld = false;
            lock (Gate)
            {
                if (killOnStart && ActiveTweens.TryGetValue(actorId, out previous))
                {
                    ActiveTweens.Remove(actorId);
                    replacingOld = true;
                }

                ActiveTweens[actorId] = tween;
            }

            UnityEngine.Debug.Log($"[TweenGate] Starting tween for actor={actorId}, replacingOld={replacingOld}");

            previous?.Kill();

            tween.OnKill(() => Release(actorId, tween));
        }

        internal static void KillInternal(int actorId, bool complete)
        {
            UnityEngine.Debug.Log($"[TweenGate] KILL active tween for actor={actorId}");

            Tween tween;
            lock (Gate)
            {
                if (!ActiveTweens.TryGetValue(actorId, out tween))
                {
                    return;
                }

                ActiveTweens.Remove(actorId);
            }

            if (tween == null)
            {
                return;
            }

            if (complete)
            {
                tween.Complete();
            }
            else
            {
                tween.Kill();
            }
        }

        private static void Release(int actorId, Tween tween)
        {
            lock (Gate)
            {
                if (ActiveTweens.TryGetValue(actorId, out var current) && current == tween)
                {
                    ActiveTweens.Remove(actorId);
                }
            }
        }

        public readonly struct TweenHandle
        {
            private readonly int actorId;

            internal TweenHandle(int actorId)
            {
                this.actorId = actorId;
            }

            public void Start(Tween tween, bool killOnStart = true)
            {
                if (actorId == 0)
                {
                    tween?.Play();
                    return;
                }

                StartInternal(actorId, tween, killOnStart);
            }

            public void KillActive(bool complete = false)
            {
                if (actorId == 0)
                {
                    return;
                }

                KillInternal(actorId, complete);
            }
        }
    }
}
