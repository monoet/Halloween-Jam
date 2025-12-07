using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace BattleV2.Core
{
    /// <summary>
    /// Captures the Unity main thread and SynchronizationContext to enforce main-thread execution.
    /// Provides helpers to assert/switch back to the Unity thread from async/Task flows.
    /// </summary>
    public static class UnityThread
    {
        private static int _mainThreadId;
        private static SynchronizationContext _unityContext;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            CaptureIfPossible("Init");
        }

        private static void CaptureIfPossible(string tag)
        {
            var ctx = SynchronizationContext.Current;
            if (ctx != null)
            {
                _unityContext = ctx;
            }

            if (_mainThreadId == 0)
            {
                _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            }
        }

        public static int MainThreadId => _mainThreadId;
        public static int CurrentThreadId => Thread.CurrentThread.ManagedThreadId;

        public static bool IsMainThread =>
            _mainThreadId != 0 && Thread.CurrentThread.ManagedThreadId == _mainThreadId;

        public static void AssertMainThread(string tag)
        {
            if (IsMainThread)
            {
                return;
            }

            throw new InvalidOperationException(
                $"EnsureRunningOnMainThread failed ({tag}). currentThread={CurrentThreadId} mainThread={_mainThreadId}");
        }

        public static Task SwitchToMainThread()
        {
            if (IsMainThread)
            {
                return Task.CompletedTask;
            }

            if (_unityContext == null)
            {
                CaptureIfPossible("SwitchToMainThread.Recapture");
            }

            if (_unityContext == null)
            {
                return Task.FromException(new InvalidOperationException(
                    $"[UnityThread] No Unity SynchronizationContext available. currentThread={CurrentThreadId} mainThread={_mainThreadId}"));
            }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            _unityContext.Post(_ =>
            {
                _mainThreadId = Thread.CurrentThread.ManagedThreadId;
                if (_unityContext == null)
                {
                    _unityContext = SynchronizationContext.Current;
                }
                tcs.TrySetResult(true);
            }, null);

            return tcs.Task;
        }
    }
}
