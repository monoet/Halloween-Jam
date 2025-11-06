using System;
using System.Collections.Generic;
using BattleV2.AnimationSystem;
using BattleV2.Core;

namespace BattleV2.AnimationSystem.Execution.Runtime
{
    internal enum StepRunStatus
    {
        Completed,
        Skipped,
        Failed,
        Branch,
        Abort
    }

    internal enum StepGroupResultStatus
    {
        Completed,
        Branch,
        Abort
    }

    internal readonly struct StepResult
    {
        private StepResult(StepRunStatus status, string branchTargetId, string abortReason)
        {
            Status = status;
            BranchTargetId = branchTargetId;
            AbortReason = abortReason;
        }

        public StepRunStatus Status { get; }
        public string BranchTargetId { get; }
        public string AbortReason { get; }

        public static StepResult Completed => new(StepRunStatus.Completed, null, null);
        public static StepResult Skipped => new(StepRunStatus.Skipped, null, null);
        public static StepResult Failed => new(StepRunStatus.Failed, null, null);
        public static StepResult Branch(string targetId) => new(StepRunStatus.Branch, targetId, null);
        public static StepResult Abort(string reason) => new(StepRunStatus.Abort, null, reason);
    }

    internal readonly struct StepGroupResult
    {
        private StepGroupResult(StepGroupResultStatus status, string branchTargetId, string abortReason)
        {
            Status = status;
            BranchTargetId = branchTargetId;
            AbortReason = abortReason;
        }

        public StepGroupResultStatus Status { get; }
        public string BranchTargetId { get; }
        public string AbortReason { get; }

        public static StepGroupResult Completed() => new(StepGroupResultStatus.Completed, null, null);
        public static StepGroupResult Branch(string targetId) => new(StepGroupResultStatus.Branch, targetId, null);
        public static StepGroupResult Abort(string reason) => new(StepGroupResultStatus.Abort, null, reason);
    }

    internal sealed class ExecutionState : IDisposable
    {
        private readonly Dictionary<string, WindowState> openWindows = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TimedHitResultEvent> windowResults = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> activeLocks = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> groupLookup = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<IDisposable> subscriptions = new();
        private readonly StepSchedulerContext context;
        private readonly ActionRecipe recipe;
        private readonly string logTag;

        public ExecutionState(ActionRecipe recipe, StepSchedulerContext context, string logTag)
        {
            this.recipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
            this.context = context;
            this.logTag = logTag ?? nameof(StepScheduler);

            if (recipe.Groups != null)
            {
                for (int i = 0; i < recipe.Groups.Count; i++)
                {
                    var id = recipe.Groups[i].Id;
                    if (!string.IsNullOrWhiteSpace(id) && !groupLookup.ContainsKey(id))
                    {
                        groupLookup[id] = i;
                    }
                }
            }
        }

        public bool AbortRequested { get; private set; }

        public void Initialize()
        {
            if (context.EventBus != null)
            {
                subscriptions.Add(context.EventBus.Subscribe<TimedHitResultEvent>(OnTimedHitResult));
            }
        }

        public void RequestAbort(string reason)
        {
            AbortRequested = true;
            BattleLogger.Warn(logTag, $"Recipe '{recipe.Id}' aborted. Reason={reason ?? "(null)"}");
        }

        public bool TryGetGroupIndex(string id, out int index)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                index = -1;
                return false;
            }

            return groupLookup.TryGetValue(id, out index);
        }

        public void RegisterWindow(string id, WindowState window)
        {
            if (string.IsNullOrWhiteSpace(id) || window == null)
            {
                return;
            }

            openWindows[id] = window;
        }

        public bool TryRemoveWindow(string id, out WindowState window)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                window = null;
                return false;
            }

            return openWindows.Remove(id, out window);
        }

        public bool TryGetWindowResult(string id, out TimedHitResultEvent result)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                result = default;
                return false;
            }

            return windowResults.Remove(id, out result);
        }

        public bool RegisterLock(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                reason = "timeline";
            }

            return activeLocks.Add(reason);
        }

        public bool ReleaseLock(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                reason = "timeline";
            }

            return activeLocks.Remove(reason);
        }

        public void ImmediateCleanup()
        {
            CleanupWindows();
            CleanupLocks();

            if (context.TimedHitService != null && context.Actor != null)
            {
                context.TimedHitService.Reset(context.Actor);
            }
        }

        private void OnTimedHitResult(TimedHitResultEvent evt)
        {
            if (evt.Actor != context.Actor)
            {
                return;
            }

            windowResults[evt.Tag] = evt;
        }

        public void Dispose()
        {
            ImmediateCleanup();

            for (int i = 0; i < subscriptions.Count; i++)
            {
                subscriptions[i]?.Dispose();
            }

            subscriptions.Clear();
        }

        private void CleanupWindows()
        {
            if (context.EventBus != null)
            {
                foreach (var window in openWindows.Values)
                {
                    context.EventBus.Publish(new AnimationWindowEvent(context.Actor, window.Tag, string.Empty, 0f, 0f, false, 0, 0));
                }
            }

            openWindows.Clear();
            windowResults.Clear();
        }

        private void CleanupLocks()
        {
            if (context.EventBus != null)
            {
                foreach (var reason in activeLocks)
                {
                    context.EventBus.Publish(new AnimationLockEvent(context.Actor, false, reason));
                }
            }

            activeLocks.Clear();
        }

        public sealed class WindowState
        {
            public WindowState(string id, string tag)
            {
                Id = id;
                Tag = tag;
            }

            public string Id { get; }
            public string Tag { get; }
        }
    }

    internal static class ListPool<T>
    {
        [ThreadStatic] private static Stack<List<T>> pool;

        public static List<T> Rent()
        {
            pool ??= new Stack<List<T>>(4);
            if (pool.Count == 0)
            {
                return new List<T>();
            }

            var list = pool.Pop();
            list.Clear();
            return list;
        }

        public static void Return(List<T> list)
        {
            if (list == null)
            {
                return;
            }

            list.Clear();
            pool ??= new Stack<List<T>>(4);
            pool.Push(list);
        }
    }
}
