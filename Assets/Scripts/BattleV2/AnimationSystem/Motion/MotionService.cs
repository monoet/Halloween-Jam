using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BattleV2.AnimationSystem.Execution.Runtime;
using BattleV2.AnimationSystem.Execution.Runtime.Core;
using BattleV2.Core;
using BattleV2.Diagnostics;
using DG.Tweening;
using UnityEngine;

namespace BattleV2.AnimationSystem.Motion
{
    /// <summary>
    /// Backend locomotion orchestrator for Route A.
    /// Owns DOTween execution, enforces a single motion per actor/binding, and commits positions explicitly.
    /// </summary>
    public sealed class MotionService
    {
        private const string ChannelLocomotion = "Locomotion";
        private readonly object sync = new();
        private readonly Dictionary<ResourceKey, MotionState> states = new();
        private readonly IMainThreadInvoker mainThreadInvoker;

        public MotionService(IMainThreadInvoker mainThreadInvoker)
        {
            this.mainThreadInvoker = mainThreadInvoker ?? MainThreadInvoker.Instance;
        }

        public ResourceKey BuildLocomotionKey(CombatantState actor, Transform motionRoot)
        {
            int actorId = actor != null ? actor.GetInstanceID() : 0;
            int bindingId = motionRoot != null ? motionRoot.GetInstanceID() : 0;
            return new ResourceKey(ChannelLocomotion, actorId, bindingId);
        }

        public Task MoveToLocalAsync(
            ResourceKey key,
            Transform motionRoot,
            Vector3 targetLocalPos,
            float durationSeconds,
            Ease ease,
            StepConflictPolicy policy,
            CancellationToken token,
            string reason = null)
        {
            if (motionRoot == null)
            {
                return Task.CompletedTask;
            }

            return RunMotionOnMainThreadAsync(
                key,
                motionRoot,
                () => CreateMoveTween(motionRoot, targetLocalPos, durationSeconds, ease),
                policy,
                token,
                reason);
        }

        public Task EnsureReturnHomeAsync(
            ResourceKey key,
            Transform motionRoot,
            float durationSeconds,
            Ease ease,
            StepConflictPolicy policy,
            CancellationToken token,
            string reason = null)
        {
            if (motionRoot == null)
            {
                return Task.CompletedTask;
            }

            EnsureHomeSnapshot(key, motionRoot);
            if (IsInTransit(key))
            {
                return ReturnHomeAsync(key, motionRoot, durationSeconds, ease, policy, token, overrideStartLocalPos: null, reason: reason);
            }

            if (!TryGetLastCommittedLocalPos(key, out var committed))
            {
                return ReturnHomeAsync(key, motionRoot, durationSeconds, ease, policy, token, overrideStartLocalPos: null, reason: reason);
            }

            MotionState state;
            lock (sync)
            {
                state = GetOrCreateState(key);
            }

            if (committed == state.HomeLocalPos)
            {
                return Task.CompletedTask;
            }

            return ReturnHomeAsync(key, motionRoot, durationSeconds, ease, policy, token, overrideStartLocalPos: null, reason: reason);
        }

        public Task ReturnHomeAsync(
            ResourceKey key,
            Transform motionRoot,
            float durationSeconds,
            Ease ease,
            StepConflictPolicy policy,
            CancellationToken token,
            Vector3? overrideStartLocalPos = null,
            string reason = null)
        {
            if (motionRoot == null)
            {
                return Task.CompletedTask;
            }

            return RunMotionOnMainThreadAsync(
                key,
                motionRoot,
                () =>
                {
                    EnsureHomeSnapshot(key, motionRoot);
                    var state = GetOrCreateState(key);
                    if (overrideStartLocalPos.HasValue)
                    {
                        motionRoot.localPosition = overrideStartLocalPos.Value;
                    }
                    return CreateMoveTween(motionRoot, state.HomeLocalPos, durationSeconds, ease);
                },
                policy,
                token,
                reason);
        }

        public void EnsureHomeSnapshot(ResourceKey key, Transform motionRoot)
        {
            if (motionRoot == null)
            {
                return;
            }

            lock (sync)
            {
                var state = GetOrCreateState(key);
                if (state.HasHome)
                {
                    return;
                }

                state.HomeLocalPos = motionRoot.localPosition;
                state.HomeLocalRot = motionRoot.localRotation;
                state.HomeLocalScale = motionRoot.localScale;
                state.LastCommittedLocalPos = state.HomeLocalPos;
                state.HasHome = true;
            }
        }

        public Vector3 WorldToLocal(Transform motionRoot, Vector3 worldPos)
        {
            if (motionRoot == null)
            {
                return worldPos;
            }

            return motionRoot.parent != null
                ? motionRoot.parent.InverseTransformPoint(worldPos)
                : worldPos;
        }

        public bool TryGetLastCommittedLocalPos(ResourceKey key, out Vector3 pos)
        {
            lock (sync)
            {
                if (states.TryGetValue(key, out var state))
                {
                    pos = state.LastCommittedLocalPos;
                    return true;
                }
            }

            pos = default;
            return false;
        }

        public bool IsInTransit(ResourceKey key)
        {
            lock (sync)
            {
                return states.TryGetValue(key, out var state) && state.IsInTransit;
            }
        }

        public void Cancel(ResourceKey key, string reason = null)
        {
            MotionExecution active;
            lock (sync)
            {
                active = GetOrCreateState(key).Active;
            }

            if (active.Completion == null || active.Completion.Task.IsCompleted)
            {
                return;
            }

            if (BattleDebug.IsEnabled("MS"))
            {
                BattleDebug.Log("MS", 2, $"Cancel requested key={key} reason={reason ?? "(none)"}");
            }

            try
            {
                active.Tween?.Kill(complete: false);
            }
            catch
            {
            }
            active.Completion.TrySetCanceled();
        }

        private Task RunMotionOnMainThreadAsync(
            ResourceKey key,
            Transform motionRoot,
            Func<Tween> tweenFactory,
            StepConflictPolicy policy,
            CancellationToken token,
            string reason)
        {
            if (tweenFactory == null)
            {
                return Task.CompletedTask;
            }

            EnsureHomeSnapshot(key, motionRoot);

            return mainThreadInvoker.RunAsync(() =>
            {
                BattleDebug.CaptureMainThread();
                if (token.IsCancellationRequested)
                {
                    return Task.FromCanceled(token);
                }

                ResolveConflictMainThread(key, policy, reason);

                if (BattleDebug.IsEnabled("MS"))
                {
                    int beforeKill = CountTweens(motionRoot);
                    BattleDebug.Log("MS", 4, $"Start key={key} reason={reason ?? "(none)"} tweensBeforeKill={beforeKill}");
                }

                DOTween.Kill(motionRoot, complete: false);

                var tween = tweenFactory();
                if (tween == null)
                {
                    Commit(key, motionRoot, completed: true);
                    return Task.CompletedTask;
                }

                var tcs = new TaskCompletionSource<bool>();

                lock (sync)
                {
                    var state = GetOrCreateState(key);
                    state.IsInTransit = true;
                    state.Active = new MotionExecution(tween, tcs);
                }

                tween.OnComplete(() =>
                {
                    bool clearActive = false;
                    lock (sync)
                    {
                        var state = GetOrCreateState(key);
                        if (state.Active.Tween == tween)
                        {
                            state.Active = default;
                            clearActive = true;
                        }
                    }

                    Commit(key, motionRoot, completed: true);

                    if (BattleDebug.IsEnabled("MS"))
                    {
                        int remaining = CountTweens(motionRoot);
                        BattleDebug.Log("MS", 3, $"Commit key={key} completed=True clearedActive={clearActive} remainingTweens={remaining} local={motionRoot.localPosition}");
                    }

                    tcs.TrySetResult(true);
                });
                tween.OnKill(() =>
                {
                    // DOTween invokes OnKill when the tween is auto-killed after completion.
                    // Ignore kill callbacks once we've already completed, otherwise we log misleading "completed=False"
                    // and risk cancelling the completion signal.
                    if (tcs.Task.IsCompleted)
                    {
                        return;
                    }

                    bool clearActive = false;
                    lock (sync)
                    {
                        var state = GetOrCreateState(key);
                        if (state.Active.Tween == tween)
                        {
                            state.Active = default;
                            clearActive = true;
                        }
                    }

                    Commit(key, motionRoot, completed: false);

                    if (BattleDebug.IsEnabled("MS"))
                    {
                        int remaining = CountTweens(motionRoot);
                        BattleDebug.Log("MS", 3, $"Commit key={key} completed=False clearedActive={clearActive} remainingTweens={remaining} local={motionRoot.localPosition}");
                    }

                    tcs.TrySetCanceled();
                });

                tween.Play();

                return tcs.Task;
            });
        }

        private void ResolveConflictMainThread(ResourceKey key, StepConflictPolicy policy, string reason)
        {
            MotionExecution active;
            lock (sync)
            {
                active = GetOrCreateState(key).Active;
            }

            if (active.Completion == null || active.Completion.Task.IsCompleted)
            {
                return;
            }

            switch (policy)
            {
                case StepConflictPolicy.SkipIfRunning:
                    if (BattleDebug.IsEnabled("MS"))
                    {
                        BattleDebug.Warn("MS", 5, $"SkipIfRunning key={key} reason={reason ?? "(none)"}");
                    }
                    return;

                case StepConflictPolicy.CancelRunning:
                    if (BattleDebug.IsEnabled("MS"))
                    {
                        BattleDebug.Log("MS", 2, $"CancelRunning key={key} reason={reason ?? "(none)"}");
                    }
                    try
                    {
                        active.Tween?.Kill(complete: false);
                    }
                    catch
                    {
                    }
                    active.Completion.TrySetCanceled();
                    return;

                case StepConflictPolicy.WaitForCompletion:
                default:
                    if (BattleDebug.IsEnabled("MS"))
                    {
                        BattleDebug.Warn("MS", 6, $"WaitForCompletion not supported in Route A yet; treating as CancelRunning. key={key}");
                    }
                    try
                    {
                        active.Tween?.Kill(complete: false);
                    }
                    catch
                    {
                    }
                    active.Completion.TrySetCanceled();
                    return;
            }
        }

        private void Commit(ResourceKey key, Transform motionRoot, bool completed)
        {
            lock (sync)
            {
                var state = GetOrCreateState(key);
                state.IsInTransit = false;
                if (motionRoot != null)
                {
                    state.LastCommittedLocalPos = motionRoot.localPosition;
                }
            }

            _ = completed;
        }

        private MotionState GetOrCreateState(ResourceKey key)
        {
            if (!states.TryGetValue(key, out var state))
            {
                state = new MotionState();
                states[key] = state;
            }

            return state;
        }

        private static Tween CreateMoveTween(Transform motionRoot, Vector3 targetLocalPos, float durationSeconds, Ease ease)
        {
            durationSeconds = Mathf.Max(0f, durationSeconds);
            var tween = motionRoot.DOLocalMove(targetLocalPos, durationSeconds);
            tween.SetEase(ease);
            return tween;
        }

        private static int CountTweens(UnityEngine.Object target)
        {
            try
            {
                var tweens = DOTween.TweensByTarget(target, true);
                return tweens != null ? tweens.Count : 0;
            }
            catch
            {
                return -1;
            }
        }

        private sealed class MotionState
        {
            public bool HasHome;
            public bool IsInTransit;
            public Vector3 HomeLocalPos;
            public Quaternion HomeLocalRot;
            public Vector3 HomeLocalScale;
            public Vector3 LastCommittedLocalPos;
            public MotionExecution Active;
        }

        private readonly struct MotionExecution
        {
            public MotionExecution(Tween tween, TaskCompletionSource<bool> completion)
            {
                Tween = tween;
                Completion = completion;
            }

            public Tween Tween { get; }
            public TaskCompletionSource<bool> Completion { get; }
        }
    }
}
