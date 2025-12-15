using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using BattleV2.Diagnostics;
using UnityEngine;

namespace BattleV2.Core
{
    /// <summary>
    /// Executes queued actions on Unity's main thread. Lightweight dispatcher for async/await integration.
    /// </summary>
    public sealed class MainThreadInvoker : MonoBehaviour, IMainThreadInvoker
    {
        private static readonly ConcurrentQueue<Func<Task>> workQueue = new ConcurrentQueue<Func<Task>>();
        private static MainThreadInvoker instance;

        public static MainThreadInvoker Instance => EnsureInstance();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            BattleDebug.CaptureMainThread();
            var ensured = EnsureInstance();
            BattleDebug.ConfigureInvoker(ensured);
        }

        private static MainThreadInvoker EnsureInstance()
        {
            if (instance != null)
            {
                return instance;
            }

            instance = FindFirstObjectByType<MainThreadInvoker>();
            if (instance != null)
            {
                DontDestroyOnLoad(instance.gameObject);
                return instance;
            }

            var go = new GameObject("[MainThreadInvoker]");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<MainThreadInvoker>();
            return instance;
        }

        private void Update()
        {
            while (workQueue.TryDequeue(out var job))
            {
                try
                {
                    var task = job();
                    if (task != null && !task.IsCompleted)
                    {
                        _ = MonitorTask(task);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[MainThreadInvoker] Job threw: {ex}");
                }
            }
        }

        private async Task MonitorTask(Task task)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MainThreadInvoker] Async job threw: {ex}");
            }
        }

        public Task RunAsync(Func<Task> action)
        {
            if (action == null)
            {
                return Task.CompletedTask;
            }

            var tcs = new TaskCompletionSource<bool>();
            workQueue.Enqueue(async () =>
            {
                try
                {
                    await action();
                    tcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            });
            return tcs.Task;
        }

        public void Run(Action action)
        {
            if (action == null)
            {
                return;
            }

            workQueue.Enqueue(() =>
            {
                action();
                return Task.CompletedTask;
            });
        }

        public Task NextFrameAsync(CancellationToken token = default)
        {
            if (token.IsCancellationRequested)
            {
                return Task.FromCanceled(token);
            }

            var tcs = new TaskCompletionSource<bool>();
            StartCoroutine(NextFrameRoutine(tcs, token));
            return tcs.Task;
        }

        private IEnumerator NextFrameRoutine(TaskCompletionSource<bool> tcs, CancellationToken token)
        {
            yield return null;
            if (token.IsCancellationRequested)
            {
                tcs.TrySetCanceled(token);
            }
            else
            {
                tcs.TrySetResult(true);
            }
        }
    }
}
