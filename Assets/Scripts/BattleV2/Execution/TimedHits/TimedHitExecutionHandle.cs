using System;
using System.Threading;
using System.Threading.Tasks;
using BattleV2.Charge;
using BattleV2.Core;

namespace BattleV2.Execution.TimedHits
{
    /// <summary>
    /// Lightweight synchronization primitive shared between the animation scheduler and the battle pipeline
    /// so timed-hit results can flow without tightly coupling both systems.
    /// </summary>
    public sealed class TimedHitExecutionHandle
    {
        private readonly TaskCompletionSource<TimedHitResult?> completionSource;

        public TimedHitExecutionHandle(TimedHitResult? initialResult = null)
        {
            completionSource = new TaskCompletionSource<TimedHitResult?>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (initialResult.HasValue)
            {
                completionSource.TrySetResult(initialResult);
            }
        }

        public bool IsCompleted => completionSource.Task.IsCompleted;

        public Task<TimedHitResult?> AsTask() => completionSource.Task;

        public Task<TimedHitResult?> WaitAsync(CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled || completionSource.Task.IsCompleted)
            {
                return completionSource.Task;
            }

            return WaitWithCancellation(cancellationToken);
        }

        public bool TrySetResult(TimedHitResult? result)
        {
            return completionSource.TrySetResult(result);
        }

        public bool TrySetCancelled(CancellationToken cancellationToken = default)
        {
            if (!cancellationToken.CanBeCanceled)
            {
                return completionSource.TrySetCanceled();
            }

            return completionSource.TrySetCanceled(cancellationToken);
        }

        public bool TrySetException(Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            return completionSource.TrySetException(exception);
        }

        private async Task<TimedHitResult?> WaitWithCancellation(CancellationToken cancellationToken)
        {
            using (cancellationToken.Register(() => completionSource.TrySetCanceled(cancellationToken), useSynchronizationContext: false))
            {
                BattleDiagnostics.Log(
                    "Thread.debug00",
                    $"[Thread.debug00][TimedWait.Enter] tid={Thread.CurrentThread.ManagedThreadId} isMain={UnityMainThreadGuard.IsMainThread()} cancelable={cancellationToken.CanBeCanceled}",
                    null);

                var result = await completionSource.Task;

                BattleDiagnostics.Log(
                    "Thread.debug00",
                    $"[Thread.debug00][TimedWait.AfterAwait] tid={Thread.CurrentThread.ManagedThreadId} isMain={UnityMainThreadGuard.IsMainThread()}",
                    null);

                return result;
            }
        }
    }
}
