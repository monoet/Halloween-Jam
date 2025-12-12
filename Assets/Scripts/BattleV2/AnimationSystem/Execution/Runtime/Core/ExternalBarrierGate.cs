using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BattleV2.Diagnostics;

namespace BattleV2.AnimationSystem.Execution.Runtime.Core
{
    /// <summary>
    /// Lets external systems (e.g. MotionService/DOTween started from observers) register in-flight work
    /// so StepScheduler can await real completion as a barrier between groups/recipes.
    /// </summary>
    public sealed class ExternalBarrierGate
    {
        /// <summary>
        /// Global bypass switch (useful for quick isolation in DEV). When disabled, awaits return immediately.
        /// </summary>
        public static bool Enabled { get; set; } = true;

        private readonly object sync = new();
        private readonly List<BarrierEntry> all = new();
        private readonly List<BarrierEntry> currentGroup = new();
        private string currentGroupId;
        private bool expectedBarrier;
        private string expectedChannel;
        private string expectedReason;

        /// <summary>
        /// DEV safety timeout (ms) to prevent hard-locking the battle loop if a task never completes.
        /// 0 disables timeout.
        /// </summary>
        public int DevTimeoutMs { get; set; } =
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            2000;
#else
            0;
#endif

        public bool StrictMode { get; set; } =
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            true;
#else
            false;
#endif

        public void BeginGroup(string groupId)
        {
            if (!Enabled)
            {
                return;
            }

            lock (sync)
            {
                currentGroupId = groupId ?? string.Empty;
                currentGroup.Clear();
                expectedBarrier = false;
                expectedChannel = null;
                expectedReason = null;
            }

            if (BattleDebug.IsEnabled("EG"))
            {
                BattleDebug.Log("EG", 1, $"BeginGroup groupId={currentGroupId}");
            }
        }

        public void BeginScope(string scopeId) => BeginGroup(scopeId);

        public void ExpectBarrier(string channel, string reason)
        {
            lock (sync)
            {
                expectedBarrier = true;
                expectedChannel = channel ?? string.Empty;
                expectedReason = reason ?? string.Empty;
            }
        }

        public void Register(Task task, ResourceKey resourceKey, string channel, string reason)
        {
            if (!Enabled)
            {
                return;
            }

            if (task == null || task.IsCompleted)
            {
                return;
            }

            BarrierEntry entry;
            int scopeCount;
            lock (sync)
            {
                entry = new BarrierEntry(
                    groupId: currentGroupId ?? string.Empty,
                    channel: channel ?? string.Empty,
                    reason: reason ?? string.Empty,
                    resourceKey: resourceKey,
                    task: task);

                currentGroup.Add(entry);
                all.Add(entry);
                scopeCount = currentGroup.Count;
            }

            if (BattleDebug.IsEnabled("EG"))
            {
                BattleDebug.Log("EG", 1, $"registered scope={entry.GroupId} channel={entry.Channel} reason={entry.Reason} key={entry.ResourceKey} count={scopeCount}");
            }
        }

        public async Task AwaitGroupAsync(CancellationToken token)
        {
            if (!Enabled)
            {
                return;
            }

            List<Task> tasks;
            string groupId;
            bool expect;
            string expectChannelSnapshot;
            string expectReasonSnapshot;

            lock (sync)
            {
                groupId = currentGroupId ?? string.Empty;
                tasks = currentGroup
                    .Where(e => e.Task != null && !e.Task.IsCompleted)
                    .Select(e => e.Task)
                    .ToList();
                expect = expectedBarrier;
                expectChannelSnapshot = expectedChannel;
                expectReasonSnapshot = expectedReason;
            }

            if (tasks.Count == 0)
            {
                if (StrictMode && expect)
                {
                    BattleDebug.Error("EG", 900, $"expected barrier missing scope={groupId} channel={expectChannelSnapshot ?? "(null)"} reason={expectReasonSnapshot ?? "(null)"}");
                }
                return;
            }

            var sw = Stopwatch.StartNew();
            try
            {
                if (BattleDebug.IsEnabled("EG"))
                {
                    BattleDebug.Log("EG", 10, $"awaiting {tasks.Count} barriers scope={groupId}");
                }

                if (DevTimeoutMs > 0)
                {
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    timeoutCts.CancelAfter(DevTimeoutMs);
                    await WhenAllWithCancellation(tasks, timeoutCts.Token).ConfigureAwait(false);
                    if (timeoutCts.IsCancellationRequested && !token.IsCancellationRequested)
                    {
                        BattleDebug.Error("EG", 910, $"TIMEOUT scope={groupId} ms={DevTimeoutMs} pending={tasks.Count}");
                        DumpPending(groupId);
                    }
                }
                else
                {
                    await WhenAllWithCancellation(tasks, token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                // DEV timeout: swallow to keep battle running.
            }
            finally
            {
                if (BattleDebug.IsEnabled("EG"))
                {
                    BattleDebug.Log("EG", 11, $"scope complete scope={groupId} count={tasks.Count} ms={sw.ElapsedMilliseconds}");
                }
            }
        }

        public Task AwaitScopeAsync(CancellationToken token) => AwaitGroupAsync(token);

        public async Task AwaitAllAsync(CancellationToken token)
        {
            if (!Enabled)
            {
                return;
            }

            List<Task> tasks;
            lock (sync)
            {
                tasks = all
                    .Where(e => e.Task != null && !e.Task.IsCompleted)
                    .Select(e => e.Task)
                    .ToList();
            }

            if (tasks.Count == 0)
            {
                return;
            }

            var sw = Stopwatch.StartNew();
            try
            {
                if (DevTimeoutMs > 0)
                {
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    timeoutCts.CancelAfter(DevTimeoutMs);
                    await WhenAllWithCancellation(tasks, timeoutCts.Token).ConfigureAwait(false);
                    if (timeoutCts.IsCancellationRequested && !token.IsCancellationRequested)
                    {
                        BattleDebug.Error("EG", 911, $"TIMEOUT awaitAll ms={DevTimeoutMs} pending={tasks.Count}");
                        DumpPending(scopeId: "(all)");
                    }
                }
                else
                {
                    await WhenAllWithCancellation(tasks, token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                // DEV timeout: swallow to keep battle running.
            }
            finally
            {
                if (BattleDebug.IsEnabled("EG"))
                {
                    BattleDebug.Log("EG", 4, $"AwaitAll done count={tasks.Count} ms={sw.ElapsedMilliseconds}");
                }
            }
        }

        private void DumpPending(string scopeId)
        {
            if (!BattleDebug.IsEnabled("EG"))
            {
                return;
            }

            List<BarrierEntry> pending;
            lock (sync)
            {
                pending = all
                    .Where(e => e.Task != null && !e.Task.IsCompleted)
                    .ToList();
            }

            for (int i = 0; i < pending.Count; i++)
            {
                var e = pending[i];
                BattleDebug.Log(
                    "EG",
                    912,
                    $"pending scope={scopeId} entryScope={e.GroupId} channel={e.Channel} reason={e.Reason} key={e.ResourceKey} status={e.Task.Status}");
            }
        }

        private static async Task WhenAllWithCancellation(List<Task> tasks, CancellationToken token)
        {
            if (tasks == null || tasks.Count == 0)
            {
                return;
            }

            var whenAll = Task.WhenAll(tasks);
            if (!token.CanBeCanceled)
            {
                await whenAll.ConfigureAwait(false);
                return;
            }

            var cancelTcs = new TaskCompletionSource<bool>();
            using var registration = token.Register(() => cancelTcs.TrySetCanceled(token));
            var completed = await Task.WhenAny(whenAll, cancelTcs.Task).ConfigureAwait(false);
            if (completed == cancelTcs.Task)
            {
                token.ThrowIfCancellationRequested();
            }

            await whenAll.ConfigureAwait(false);
        }

        private readonly struct BarrierEntry
        {
            public BarrierEntry(string groupId, string channel, string reason, ResourceKey resourceKey, Task task)
            {
                GroupId = groupId;
                Channel = channel;
                Reason = reason;
                ResourceKey = resourceKey;
                Task = task;
            }

            public string GroupId { get; }
            public string Channel { get; }
            public string Reason { get; }
            public ResourceKey ResourceKey { get; }
            public Task Task { get; }
        }
    }
}
