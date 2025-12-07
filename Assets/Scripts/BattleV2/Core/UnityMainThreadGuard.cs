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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Capture()
        {
            MainThreadId = Thread.CurrentThread.ManagedThreadId;

            BattleDiagnostics.Log(
                "Thread.debug00",
                $"[Thread.debug00][Capture] mainTid={MainThreadId} syncCtx={(SynchronizationContext.Current?.GetType().Name ?? "null")}",
                null);
        }

        public static bool IsMainThread() => Thread.CurrentThread.ManagedThreadId == MainThreadId;
    }
}
