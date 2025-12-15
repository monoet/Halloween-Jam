using System.Threading;
using UnityEngine;

namespace BattleV2.Core
{
    /// <summary>
    /// Captures the Unity main thread id early for diagnostics. Logging is normalized under Thread.debug00.
    /// </summary>
    public static class UnityMainThreadGuard
    {
        public static int MainThreadId { get; private set; } = -1;
        private static SynchronizationContext capturedContext;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Capture()
        {
            MainThreadId = Thread.CurrentThread.ManagedThreadId;
            capturedContext = SynchronizationContext.Current;

            BattleDiagnostics.Log(
                "Thread.debug00",
                $"[Thread.debug00][Capture] mainTid={MainThreadId} syncCtx={(SynchronizationContext.Current?.GetType().Name ?? "null")}",
                null);
        }

        public static bool IsMainThread() => Thread.CurrentThread.ManagedThreadId == MainThreadId;

        public static void RefreshIfStale()
        {
            var currentCtx = SynchronizationContext.Current;
            if (currentCtx == null)
            {
                return;
            }

            // Solo recapturar si estamos en el hilo que creemos principal (o aún no se capturó).
            int tid = Thread.CurrentThread.ManagedThreadId;
            bool canCapture = MainThreadId == -1 || tid == MainThreadId;
            if (!canCapture)
            {
                return;
            }

            if (capturedContext == null)
            {
                MainThreadId = tid;
                capturedContext = currentCtx;
                return;
            }

            if (!ReferenceEquals(capturedContext, currentCtx))
            {
                MainThreadId = tid;
                capturedContext = currentCtx;
            }
        }
    }
}
