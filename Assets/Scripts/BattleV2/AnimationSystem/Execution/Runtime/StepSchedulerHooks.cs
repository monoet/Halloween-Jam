using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BattleV2.AnimationSystem.Execution.Runtime
{
    /// <summary>
    /// Lightweight helper that lets systems await recipe/group completion without extending StepScheduler.
    /// </summary>
    public sealed class StepSchedulerHooks : IStepSchedulerObserver, IDisposable
    {
        private readonly StepScheduler scheduler;
        private readonly object gate = new object();
        private readonly Dictionary<GroupKey, List<TaskCompletionSource<bool>>> groupWaiters = new Dictionary<GroupKey, List<TaskCompletionSource<bool>>>();
        private readonly Dictionary<RecipeKey, List<TaskCompletionSource<bool>>> recipeWaiters = new Dictionary<RecipeKey, List<TaskCompletionSource<bool>>>();
        private readonly Dictionary<LifecycleKey, List<TaskCompletionSource<bool>>> lifecycleWaiters = new Dictionary<LifecycleKey, List<TaskCompletionSource<bool>>>();

        public StepSchedulerHooks(StepScheduler scheduler)
        {
            this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            scheduler.RegisterObserver(this);
            scheduler.LifecycleEvent += OnLifecycleEvent;
        }

        public Task WaitForGroupAsync(string groupId, CombatantState actor = null)
        {
            if (string.IsNullOrWhiteSpace(groupId))
            {
                return Task.CompletedTask;
            }

            var key = new GroupKey(groupId, ResolveActorId(actor));
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (gate)
            {
                if (!groupWaiters.TryGetValue(key, out var list))
                {
                    list = new List<TaskCompletionSource<bool>>();
                    groupWaiters[key] = list;
                }

                list.Add(tcs);
            }

            return tcs.Task;
        }

        public Task WaitForRecipeAsync(string recipeId, CombatantState actor = null)
        {
            if (string.IsNullOrWhiteSpace(recipeId))
            {
                return Task.CompletedTask;
            }

            var key = new RecipeKey(recipeId, ResolveActorId(actor));
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (gate)
            {
                if (!recipeWaiters.TryGetValue(key, out var list))
                {
                    list = new List<TaskCompletionSource<bool>>();
                    recipeWaiters[key] = list;
                }

                list.Add(tcs);
            }

            return tcs.Task;
        }

        public Task WaitForLifecycleAsync(string eventId, CombatantState actor = null)
        {
            if (string.IsNullOrWhiteSpace(eventId))
            {
                return Task.CompletedTask;
            }

            var key = new LifecycleKey(eventId, ResolveActorId(actor));
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (gate)
            {
                if (!lifecycleWaiters.TryGetValue(key, out var list))
                {
                    list = new List<TaskCompletionSource<bool>>();
                    lifecycleWaiters[key] = list;
                }

                list.Add(tcs);
            }

            return tcs.Task;
        }

        public void Dispose()
        {
            scheduler.LifecycleEvent -= OnLifecycleEvent;
            scheduler.UnregisterObserver(this);
        }

        public void OnRecipeStarted(ActionRecipe recipe, StepSchedulerContext context) { }

        public void OnRecipeCompleted(RecipeExecutionReport report, StepSchedulerContext context)
        {
            if (report.Recipe == null)
            {
                return;
            }

            CompleteRecipeWaiters(report.Recipe.Id, ResolveActorId(context.Actor));
        }

        public void OnGroupStarted(ActionStepGroup group, StepSchedulerContext context) { }

        public void OnGroupCompleted(StepGroupExecutionReport report, StepSchedulerContext context)
        {
            if (report.Group == null)
            {
                return;
            }

            CompleteGroupWaiters(report.Group.Id, ResolveActorId(context.Actor));
        }

        public void OnBranchTaken(string sourceId, string targetId, StepSchedulerContext context) { }
        public void OnStepStarted(ActionStep step, StepSchedulerContext context) { }
        public void OnStepCompleted(StepExecutionReport report, StepSchedulerContext context) { }

        private void OnLifecycleEvent(ActionLifecycleEventArgs args)
        {
            if (args.EventId == null)
            {
                return;
            }

            CompleteLifecycleWaiters(args.EventId, ResolveActorId(args.Actor));
        }

        private void CompleteGroupWaiters(string groupId, int actorId)
        {
            if (string.IsNullOrWhiteSpace(groupId))
            {
                return;
            }

            CompleteWaiters(groupWaiters, new GroupKey(groupId, actorId));
            CompleteWaiters(groupWaiters, new GroupKey(groupId, 0));
        }

        private void CompleteRecipeWaiters(string recipeId, int actorId)
        {
            if (string.IsNullOrWhiteSpace(recipeId))
            {
                return;
            }

            CompleteWaiters(recipeWaiters, new RecipeKey(recipeId, actorId));
            CompleteWaiters(recipeWaiters, new RecipeKey(recipeId, 0));
        }

        private void CompleteLifecycleWaiters(string eventId, int actorId)
        {
            if (string.IsNullOrWhiteSpace(eventId))
            {
                return;
            }

            CompleteWaiters(lifecycleWaiters, new LifecycleKey(eventId, actorId));
            CompleteWaiters(lifecycleWaiters, new LifecycleKey(eventId, 0));
        }

        private void CompleteWaiters<TKey>(Dictionary<TKey, List<TaskCompletionSource<bool>>> table, TKey key)
        {
            List<TaskCompletionSource<bool>> pending = null;

            lock (gate)
            {
                if (!table.TryGetValue(key, out pending))
                {
                    return;
                }

                table.Remove(key);
            }

            for (int i = 0; i < pending.Count; i++)
            {
                pending[i].TrySetResult(true);
            }
        }

        private static int ResolveActorId(CombatantState actor)
        {
            return actor != null ? actor.GetInstanceID() : 0;
        }

        private readonly struct GroupKey : IEquatable<GroupKey>
        {
            public GroupKey(string id, int actorId)
            {
                Id = id ?? string.Empty;
                ActorId = actorId;
            }

            public string Id { get; }
            public int ActorId { get; }

            public bool Equals(GroupKey other) => ActorId == other.ActorId && string.Equals(Id, other.Id, StringComparison.OrdinalIgnoreCase);

            public override bool Equals(object obj) => obj is GroupKey other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(ActorId, StringComparer.OrdinalIgnoreCase.GetHashCode(Id));
        }

        private readonly struct RecipeKey : IEquatable<RecipeKey>
        {
            public RecipeKey(string id, int actorId)
            {
                Id = id ?? string.Empty;
                ActorId = actorId;
            }

            public string Id { get; }
            public int ActorId { get; }

            public bool Equals(RecipeKey other) => ActorId == other.ActorId && string.Equals(Id, other.Id, StringComparison.OrdinalIgnoreCase);

            public override bool Equals(object obj) => obj is RecipeKey other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(ActorId, StringComparer.OrdinalIgnoreCase.GetHashCode(Id));
        }

        private readonly struct LifecycleKey : IEquatable<LifecycleKey>
        {
            public LifecycleKey(string eventId, int actorId)
            {
                EventId = eventId ?? string.Empty;
                ActorId = actorId;
            }

            public string EventId { get; }
            public int ActorId { get; }

            public bool Equals(LifecycleKey other) => ActorId == other.ActorId && string.Equals(EventId, other.EventId, StringComparison.OrdinalIgnoreCase);

            public override bool Equals(object obj) => obj is LifecycleKey other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(ActorId, StringComparer.OrdinalIgnoreCase.GetHashCode(EventId));
        }
    }
}
