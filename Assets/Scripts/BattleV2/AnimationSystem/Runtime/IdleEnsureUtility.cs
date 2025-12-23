using System.Collections.Generic;
using System.Threading.Tasks;
using BattleV2.Core;
using BattleV2.Orchestration.Runtime;

namespace BattleV2.AnimationSystem.Runtime
{
    internal static class IdleEnsureUtility
    {
        private static readonly object Gate = new();
        private static readonly Dictionary<int, long> PendingTokenByWrapper = new();
        private static long nextToken;

        public static void EnsureIdleNextTick(CombatantState actor, string reason)
        {
            if (actor == null)
            {
                return;
            }

            if (!AnimatorRegistry.Instance.TryGetWrapper(actor, out var wrapper) || wrapper is not AnimatorWrapper aw)
            {
                return;
            }

            EnsureIdleNextTick(aw, reason);
        }

        public static void EnsureIdleNextTick(AnimatorWrapper wrapper, string reason)
        {
            if (wrapper == null || !wrapper.isActiveAndEnabled)
            {
                return;
            }

            long token;
            int id = wrapper.GetInstanceID();
            lock (Gate)
            {
                token = ++nextToken;
                PendingTokenByWrapper[id] = token;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (BattleDiagnostics.DevFlowTrace)
            {
                BattleDiagnostics.Log(
                    "BATTLEFLOW",
                    $"IDLE_ENSURE_SCHEDULED actor={wrapper.name} token={token} reason={(string.IsNullOrWhiteSpace(reason) ? "(none)" : reason)}",
                    context: null);
            }
#endif

            var invoker = MainThreadInvoker.Instance;
            if (invoker != null)
            {
                _ = invoker.RunAsync(async () =>
                {
                    await Task.Yield();
                    ApplyIfCurrent(wrapper, id, token, reason);
                });
            }
            else
            {
                _ = EnsureIdleAsync(wrapper, id, token, reason);
            }
        }

        private static async Task EnsureIdleAsync(AnimatorWrapper wrapper, int wrapperId, long token, string reason)
        {
            await Task.Yield();
            ApplyIfCurrent(wrapper, wrapperId, token, reason);
        }

        private static void ApplyIfCurrent(AnimatorWrapper wrapper, int wrapperId, long token, string reason)
        {
            if (wrapper == null || !wrapper || !wrapper.isActiveAndEnabled)
            {
                return;
            }

            lock (Gate)
            {
                if (!PendingTokenByWrapper.TryGetValue(wrapperId, out var current) || current != token)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (BattleDiagnostics.DevFlowTrace)
                    {
                        BattleDiagnostics.Log(
                            "BATTLEFLOW",
                            $"IDLE_ENSURE_SKIPPED actor={wrapper.name} token={token} current={current} why=Superseded",
                            context: null);
                    }
#endif
                    return;
                }

                PendingTokenByWrapper.Remove(wrapperId);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (BattleDiagnostics.DevFlowTrace)
            {
                BattleDiagnostics.Log(
                    "BATTLEFLOW",
                    $"IDLE_ENSURE_APPLY actor={wrapper.name} token={token} reason={(string.IsNullOrWhiteSpace(reason) ? "(none)" : reason)}",
                    context: null);
            }
#endif
            wrapper.RequestIdleLoop(string.IsNullOrWhiteSpace(reason) ? "IdleEnsure.NextTick" : $"IdleEnsure.NextTick:{reason}");
        }
    }
}

