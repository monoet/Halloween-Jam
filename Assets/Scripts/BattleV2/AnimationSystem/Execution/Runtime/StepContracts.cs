using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using BattleV2.AnimationSystem;
using BattleV2.AnimationSystem.Runtime.Internal;
using BattleV2.AnimationSystem.Timelines;
using BattleV2.Core;
using BattleV2.Orchestration.Runtime;
using BattleV2.Execution.TimedHits;
using UnityEngine;

namespace BattleV2.AnimationSystem.Execution.Runtime
{
    /// <summary>
    /// Determines how the scheduler reacts when a new step targets an executor that is currently running.
    /// </summary>
    public enum StepConflictPolicy
    {
        /// <summary>Wait for the running execution to complete before starting the new step.</summary>
        WaitForCompletion = 0,

        /// <summary>Cancel the running execution, then start the new step.</summary>
        CancelRunning = 1,

        /// <summary>Skip the new step when the executor is already running.</summary>
        SkipIfRunning = 2
    }

    /// <summary>
    /// Defines how the steps inside a group should be executed.
    /// </summary>
    public enum StepGroupExecutionMode
    {
        Sequential = 0,
        Parallel = 1
    }

    /// <summary>
    /// Lightweight key/value store used by step payloads.
    /// Provides typed access helpers for executors.
    /// </summary>
    public readonly struct ActionStepParameters
    {
        private readonly IReadOnlyDictionary<string, string> data;

        public ActionStepParameters(IReadOnlyDictionary<string, string> data)
        {
            this.data = data;
        }

        public IReadOnlyDictionary<string, string> Data => data ?? (IReadOnlyDictionary<string, string>)EmptyDictionary.Instance;

        public bool IsEmpty => data == null || data.Count == 0;

        public bool TryGetString(string key, out string value)
        {
            if (data != null && data.TryGetValue(key, out value))
            {
                return true;
            }

            value = null;
            return false;
        }

        public bool TryGetFloat(string key, out float value)
        {
            value = default;
            if (!TryGetString(key, out var raw))
            {
                return false;
            }

            return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        public bool TryGetDouble(string key, out double value)
        {
            value = default;
            if (!TryGetString(key, out var raw))
            {
                return false;
            }

            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        public bool TryGetInt(string key, out int value)
        {
            value = default;
            if (!TryGetString(key, out var raw))
            {
                return false;
            }

            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        public bool TryGetBool(string key, out bool value)
        {
            value = default;
            if (!TryGetString(key, out var raw))
            {
                return false;
            }

            return bool.TryParse(raw, out value);
        }

        private sealed class EmptyDictionary : IReadOnlyDictionary<string, string>
        {
            public static readonly EmptyDictionary Instance = new();

            public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
            {
                yield break;
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

            public int Count => 0;
            public bool ContainsKey(string key) => false;
            public bool TryGetValue(string key, out string value)
            {
                value = null;
                return false;
            }

            public string this[string key] => null;

            public IEnumerable<string> Keys
            {
                get { yield break; }
            }

            public IEnumerable<string> Values
            {
                get { yield break; }
            }
        }
    }

    /// <summary>
    /// Atomic playback unit to be executed by a registered executor.
    /// </summary>
    public readonly struct ActionStep
    {
        public ActionStep(
            string executorId,
            string bindingId,
            ActionStepParameters parameters,
            StepConflictPolicy conflictPolicy = StepConflictPolicy.WaitForCompletion,
            string id = null,
            float delaySeconds = 0f,
            bool hasExplicitConflictPolicy = false)
        {
            if (string.IsNullOrWhiteSpace(executorId))
            {
                throw new ArgumentException("Executor id cannot be null or empty.", nameof(executorId));
            }

            ExecutorId = executorId;
            BindingId = bindingId;
            Parameters = parameters;
            ConflictPolicy = conflictPolicy;
            Id = id;
            DelaySeconds = Mathf.Max(0f, delaySeconds);
            HasExplicitConflictPolicy = hasExplicitConflictPolicy;
        }

        public string Id { get; }
        public string ExecutorId { get; }
        public string BindingId { get; }
        public ActionStepParameters Parameters { get; }
        public StepConflictPolicy ConflictPolicy { get; }
        public float DelaySeconds { get; }
        public bool HasExplicitConflictPolicy { get; }

        public bool HasBinding => !string.IsNullOrWhiteSpace(BindingId);
        public bool HasDelay => DelaySeconds > 0f;
    }

    /// <summary>
    /// Determines how a parallel group resolves completion.
    /// </summary>
    public enum StepGroupJoinPolicy
    {
        All = 0,
        Any = 1
    }

    /// <summary>
    /// Collection of steps that should be executed under a shared policy (sequential or parallel).
    /// </summary>
    public sealed class ActionStepGroup
    {
        public ActionStepGroup(
            string id,
            IReadOnlyList<ActionStep> steps,
            StepGroupExecutionMode executionMode = StepGroupExecutionMode.Sequential,
            StepGroupJoinPolicy joinPolicy = StepGroupJoinPolicy.Any,
            float timeoutSeconds = 0f)
        {
            if (steps == null || steps.Count == 0)
            {
                throw new ArgumentException("ActionStepGroup requires at least one step.", nameof(steps));
            }

            Id = id;
            Steps = steps;
            ExecutionMode = executionMode;
            JoinPolicy = joinPolicy;
            TimeoutSeconds = Mathf.Max(0f, timeoutSeconds);
        }

        public string Id { get; }
        public IReadOnlyList<ActionStep> Steps { get; }
        public StepGroupExecutionMode ExecutionMode { get; }
        public StepGroupJoinPolicy JoinPolicy { get; }
        public float TimeoutSeconds { get; }
        public bool HasTimeout => TimeoutSeconds > 0f;
    }

    /// <summary>
    /// High-level recipe composed of step groups. Represents the full behaviour requested by a timeline payload.
    /// </summary>
    public sealed class ActionRecipe
    {
        public static readonly ActionRecipe Empty = new ActionRecipe("(empty)", Array.Empty<ActionStepGroup>());

        public ActionRecipe(string id, IReadOnlyList<ActionStepGroup> groups)
        {
            Id = string.IsNullOrWhiteSpace(id) ? "(unnamed)" : id;
            Groups = groups ?? Array.Empty<ActionStepGroup>();
        }

        public string Id { get; }
        public IReadOnlyList<ActionStepGroup> Groups { get; }

        public bool IsEmpty => Groups == null || Groups.Count == 0;
    }

    /// <summary>
    /// Shared environment passed to the scheduler that contains actor-specific services.
    /// </summary>
    public readonly struct StepSchedulerContext
    {
        public StepSchedulerContext(
            AnimationRequest request,
            ActionTimeline timeline,
            IAnimationWrapper wrapper,
            IAnimationBindingResolver bindingResolver,
            AnimationRouterBundle routerBundle,
            IAnimationEventBus eventBus,
            ITimedHitService timedHitService,
            ITimedHitRunner timedHitRunner,
            bool skipResetToFallback = false)
        {
            Request = request;
            Timeline = timeline;
            Wrapper = wrapper;
            BindingResolver = bindingResolver;
            RouterBundle = routerBundle;
            EventBus = eventBus;
            TimedHitService = timedHitService;
            TimedHitRunner = timedHitRunner;
            SkipResetToFallback = skipResetToFallback;
        }

        public AnimationRequest Request { get; }
        public ActionTimeline Timeline { get; }
        public IAnimationWrapper Wrapper { get; }
        public IAnimationBindingResolver BindingResolver { get; }
        public AnimationRouterBundle RouterBundle { get; }
        public IAnimationEventBus EventBus { get; }
        public ITimedHitService TimedHitService { get; }
        public ITimedHitRunner TimedHitRunner { get; }
        public bool SkipResetToFallback { get; }

        public CombatantState Actor => Request.Actor;
    }

    /// <summary>
    /// Context provided to individual step executors.
    /// </summary>
    public readonly struct StepExecutionContext
    {
        private readonly StepSchedulerContext schedulerContext;

        public StepExecutionContext(
            StepSchedulerContext schedulerContext,
            ActionStep step,
            CancellationToken cancellationToken)
        {
            this.schedulerContext = schedulerContext;
            Step = step;
            CancellationToken = cancellationToken;
        }

        public ActionStep Step { get; }
        public AnimationRequest Request => schedulerContext.Request;
        public ActionTimeline Timeline => schedulerContext.Timeline;
        public IAnimationWrapper Wrapper => schedulerContext.Wrapper;
        public IAnimationBindingResolver Bindings => schedulerContext.BindingResolver;
        public AnimationRouterBundle RouterBundle => schedulerContext.RouterBundle;
        public CombatantState Actor => schedulerContext.Actor;
        public ITimedHitService TimedHitService => schedulerContext.TimedHitService;
        public ITimedHitRunner TimedHitRunner => schedulerContext.TimedHitRunner;
        public bool SkipResetToFallback => schedulerContext.SkipResetToFallback;
        public CancellationToken CancellationToken { get; }
    }
}
