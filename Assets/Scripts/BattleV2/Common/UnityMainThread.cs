using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace BattleV2.Common
{
    public static class UnityMainThread
    {
        private static SynchronizationContext context;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            context = SynchronizationContext.Current;
        }

        public static Task SwitchAsync()
        {
            var target = context ?? SynchronizationContext.Current;
            if (target == null || SynchronizationContext.Current == target)
            {
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource<bool>();
            target.Post(_ => tcs.SetResult(true), null);
            return tcs.Task;
        }
    }
}
