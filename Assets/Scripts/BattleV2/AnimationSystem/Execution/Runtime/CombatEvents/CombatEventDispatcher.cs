using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BattleV2.Core;
using BattleV2.Orchestration.Runtime;
using BattleV2.Providers;
using UnityEngine;
using RuntimeAnimatorWrapper = BattleV2.Orchestration.Runtime.AnimatorWrapper;

namespace BattleV2.AnimationSystem.Execution.Runtime.CombatEvents
{
    /// <summary>
    /// Bridges StepScheduler events with presentation listeners interested in combat flags (windup, runup, etc.).
    /// </summary>
    public sealed class CombatEventDispatcher : IStepSchedulerObserver
    {
        private const string LogTag = "CombatEvents";
        private const string DebugScope = "CombatEvents/Tween";

        private const bool EnablePerTargetEmission = true; // TODO: promote to config flag once staggered AoE beats land.

        private static readonly string[] GroupFlags =
        {
            CombatEventFlags.Windup,
            CombatEventFlags.Runup,
            CombatEventFlags.Impact,
            CombatEventFlags.Runback
        };

        private static readonly IReadOnlyList<string> EmptyTags = Array.Empty<string>();
        private static readonly IReadOnlyList<string> PersistTransformTag = new[] { "persist_transform" };

        private readonly List<ICombatEventListener> listeners = new List<ICombatEventListener>();
        private readonly object listenerGate = new object();
        private readonly IMainThreadInvoker mainThreadInvoker;
        private readonly Func<CombatantState, CombatantAlignment> alignmentResolver;

        private volatile int listenerCount;

        public CombatEventDispatcher(
            IMainThreadInvoker mainThreadInvoker,
            Func<CombatantState, CombatantAlignment> alignmentResolver = null)
        {
            this.mainThreadInvoker = mainThreadInvoker;
            this.alignmentResolver = alignmentResolver;
        }

        public void RegisterListener(ICombatEventListener listener)
        {
            if (listener == null)
            {
                return;
            }

            lock (listenerGate)
            {
                if (listeners.Contains(listener))
                {
                    return;
                }

                listeners.Add(listener);
                Volatile.Write(ref listenerCount, listeners.Count);
            }
        }

        public void UnregisterListener(ICombatEventListener listener)
        {
            if (listener == null)
            {
                return;
            }

            lock (listenerGate)
            {
                if (listeners.Remove(listener))
                {
                    Volatile.Write(ref listenerCount, listeners.Count);
                }
            }
        }

        /// <summary>
        /// Emits a combat flag from external callers (non-scheduler) using a minimal context
        /// built from the provided actor and optional single target.
        /// </summary>
        public void EmitExternalFlag(
            string flagId,
            CombatantState actor,
            CombatantState target = null,
            string weaponKind = "none",
            string element = "neutral",
            bool isCritical = false,
            int targetCount = 1)
        {
            if (string.IsNullOrWhiteSpace(flagId) || actor == null || Volatile.Read(ref listenerCount) == 0)
            {
                return;
            }

            var root = actor.transform;
            var actorView = new CombatEventContext.ActorView(
                actor.GetInstanceID(),
                ResolveAlignment(actor, assumeAlly: true),
                actor,
                root,
                root);

            var actionView = new CombatEventContext.ActionView(
                actionId: "timed_hit",
                family: "timed_hit",
                weaponKind: string.IsNullOrWhiteSpace(weaponKind) ? "none" : weaponKind,
                element: string.IsNullOrWhiteSpace(element) ? "neutral" : element,
                recipeId: null,
                staggerStepSeconds: 0f);

            var refs = new List<CombatEventContext.CombatantRef>(Math.Max(1, targetCount));
            if (target != null)
            {
                refs.Add(new CombatEventContext.CombatantRef(
                    target.GetInstanceID(),
                    ResolveAlignment(target, assumeAlly: false),
                    target,
                    target.transform,
                    null));
            }
            else
            {
                // if no explicit target, still reflect target count for params
                for (int i = 0; i < targetCount; i++)
                {
                    refs.Add(default);
                }
            }

            var ctx = CombatEventContext.Acquire();
            ctx.Populate(actorView, actionView, refs, perTarget: false, tags: isCritical ? new[] { "crit" } : Array.Empty<string>());

            var list = new List<CombatEventContext>(1) { ctx };
            Debug.Log($"CED01 [CombatEventDispatcher] EmitExternalFlag flag='{flagId}' actor={actor.name} targets={refs.Count}", actor);
            Dispatch(flagId, list);
        }

        public void OnRecipeStarted(ActionRecipe recipe, StepSchedulerContext context)
        {
        }

        public void OnRecipeCompleted(RecipeExecutionReport report, StepSchedulerContext context)
        {
            if (report.Cancelled)
            {
                EmitFlag(CombatEventFlags.ActionCancel, context, perTargetOverride: false);
            }
        }

        public void OnGroupStarted(ActionStepGroup group, StepSchedulerContext context)
        {
            if (group == null || string.IsNullOrWhiteSpace(group.Id))
            {
                return;
            }

            if (TryResolveFlag(group.Id, out var flagId))
            {
                bool perTarget = ShouldEmitPerTarget(flagId, context);
                EmitFlag(flagId, context, perTarget);
            }
        }

        public void OnGroupCompleted(StepGroupExecutionReport report, StepSchedulerContext context)
        {
        }

        public void OnBranchTaken(string sourceId, string targetId, StepSchedulerContext context)
        {
        }

        public void OnStepStarted(ActionStep step, StepSchedulerContext context)
        {
        }

        public void OnStepCompleted(StepExecutionReport report, StepSchedulerContext context)
        {
        }

        public void EmitActionCancel(StepSchedulerContext schedulerContext)
        {
            if (Volatile.Read(ref listenerCount) == 0)
            {
                return;
            }

            var contexts = BuildContexts(CombatEventFlags.ActionCancel, schedulerContext, perTargetOverride: false);
            if (contexts == null || contexts.Count == 0)
            {
                return;
            }

            Dispatch(CombatEventFlags.ActionCancel, contexts);
        }

        private void EmitFlag(string flagId, StepSchedulerContext schedulerContext, bool perTargetOverride)
        {
            if (string.IsNullOrWhiteSpace(flagId) || Volatile.Read(ref listenerCount) == 0)
            {
                return;
            }

            var contexts = BuildContexts(flagId, schedulerContext, perTargetOverride);
            if (contexts == null || contexts.Count == 0)
            {
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var actorName = schedulerContext.Request.Actor != null
                ? schedulerContext.Request.Actor.name
                : "(null)";
            Debug.Log($"[{DebugScope}] Emitting '{flagId}' for actor '{actorName}' contexts={contexts.Count}");
#endif

            bool perTarget = contexts[0]?.Targets.PerTarget ?? false;
            float staggerStep = contexts[0]?.Action.StaggerStepSeconds ?? 0f;
            if (perTarget && contexts.Count > 1 && staggerStep > 0f)
            {
                DispatchWithStagger(flagId, contexts, staggerStep);
                return;
            }

            Dispatch(flagId, contexts);
        }

        private List<CombatEventContext> BuildContexts(string flagId, StepSchedulerContext schedulerContext, bool perTargetOverride)
        {
            var request = schedulerContext.Request;
            var actor = request.Actor;
            if (actor == null)
            {
                return null;
            }

            var root = actor.transform;
            var anchor = (schedulerContext.Wrapper as RuntimeAnimatorWrapper)?.AnimatedRoot;
            if (anchor == null)
            {
                anchor = root;
            }

            var actorView = new CombatEventContext.ActorView(
                actor.GetInstanceID(),
                ResolveAlignment(actor, assumeAlly: true),
                actor,
                root,
                anchor);

            var selection = request.Selection;
            var actionView = new CombatEventContext.ActionView(
                ResolveActionId(selection, schedulerContext),
                ResolveFamily(selection, schedulerContext),
                ResolveWeapon(selection),
                ResolveElement(selection),
                ResolveRecipeId(selection, schedulerContext),
                ResolveStaggerStep(selection));

            var targets = request.Targets ?? Array.Empty<CombatantState>();
            var refs = new List<CombatEventContext.CombatantRef>(targets.Count);
            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null)
                {
                    continue;
                }

                refs.Add(new CombatEventContext.CombatantRef(
                    target.GetInstanceID(),
                    ResolveAlignment(target, assumeAlly: false),
                    target,
                    target.transform,
                    null));
            }

            bool requestPerTarget = (perTargetOverride || ShouldEmitPerTarget(selection, targets)) && refs.Count > 1;
            bool perTarget = EnablePerTargetEmission && requestPerTarget;
            // NOTE: Minimal AoE support dispatches contexts per target but does not yet handle stagger_step delays.
            // TODO: incorporate timing offsets from selection.Action.stagger_step once scheduler exposes it.
            var tags = ResolveTags(flagId);

            var contexts = new List<CombatEventContext>(perTarget ? refs.Count : 1);
            if (!perTarget)
            {
                var ctx = CombatEventContext.Acquire();
                ctx.Populate(actorView, actionView, refs, false, tags);
                contexts.Add(ctx);
                return contexts;
            }

            var singleTargetBuffer = new List<CombatEventContext.CombatantRef>(1);
            for (int i = 0; i < refs.Count; i++)
            {
                singleTargetBuffer.Clear();
                singleTargetBuffer.Add(refs[i]);

                var ctx = CombatEventContext.Acquire();
                ctx.Populate(actorView, actionView, singleTargetBuffer, true, tags);
                contexts.Add(ctx);
            }

            return contexts;
        }

        private static string ResolveActionId(BattleSelection selection, StepSchedulerContext schedulerContext)
        {
            if (selection.Action != null && !string.IsNullOrWhiteSpace(selection.Action.id))
            {
                return selection.Action.id;
            }

            if (schedulerContext.Timeline != null && !string.IsNullOrWhiteSpace(schedulerContext.Timeline.ActionId))
            {
                return schedulerContext.Timeline.ActionId;
            }

            return "(action)";
        }

        private static string ResolveFamily(BattleSelection selection, StepSchedulerContext schedulerContext)
        {
            if (selection.Action != null && !string.IsNullOrWhiteSpace(selection.Action.id))
            {
                return selection.Action.id;
            }

            if (schedulerContext.Timeline != null && !string.IsNullOrWhiteSpace(schedulerContext.Timeline.ActionId))
            {
                return schedulerContext.Timeline.ActionId;
            }

            return "attack/basic";
        }

        private static string ResolveWeapon(BattleSelection selection) => "none";

        private static string ResolveElement(BattleSelection selection) => "neutral";

        private static string ResolveRecipeId(BattleSelection selection, StepSchedulerContext schedulerContext)
        {
            if (!string.IsNullOrWhiteSpace(selection.AnimationRecipeId))
            {
                return selection.AnimationRecipeId;
            }

            if (schedulerContext.Timeline != null && !string.IsNullOrWhiteSpace(schedulerContext.Timeline.ActionId))
            {
                return schedulerContext.Timeline.ActionId;
            }

            return null;
        }

        private static float ResolveStaggerStep(BattleSelection selection)
        {
            if (selection.Action != null)
            {
                return Mathf.Max(0f, selection.Action.combatEventStaggerStep);
            }

            return 0f;
        }

        private static bool ShouldEmitPerTarget(string flagId, StepSchedulerContext context)
        {
            if (!string.Equals(flagId, CombatEventFlags.Impact, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return ShouldEmitPerTarget(context.Request.Selection, context.Request.Targets);
        }

        private static bool ShouldEmitPerTarget(BattleSelection selection, IReadOnlyList<CombatantState> resolvedTargets)
        {
            if (selection.Targets.HasValue)
            {
                var set = selection.Targets.Value;
                if (set.IsGroup)
                {
                    return true;
                }
            }

            return resolvedTargets != null && resolvedTargets.Count > 1;
        }

        private IReadOnlyList<string> ResolveTags(string flagId)
        {
            if (string.Equals(flagId, CombatEventFlags.Runup, StringComparison.OrdinalIgnoreCase))
            {
                return PersistTransformTag;
            }

            return EmptyTags;
        }

        private CombatantAlignment ResolveAlignment(CombatantState combatant, bool assumeAlly)
        {
            if (combatant == null)
            {
                return CombatantAlignment.Unknown;
            }

            if (alignmentResolver != null)
            {
                try
                {
                    var resolved = alignmentResolver(combatant);
                    if (resolved != CombatantAlignment.Unknown)
                    {
                        return resolved;
                    }
                }
                catch (Exception ex)
            {
                BattleLogger.Warn(LogTag, $"Alignment resolver threw for '{combatant.name}': {ex.Message}");
            }
        }

            // TODO: replace with roster-aware resolver once sides are exposed in gameplay data.
            return assumeAlly ? CombatantAlignment.Ally : CombatantAlignment.Enemy;
        }

        private void Dispatch(string flagId, List<CombatEventContext> contexts)
        {
            if (contexts == null || contexts.Count == 0)
            {
                return;
            }

            void Invoke()
            {
                BattleDiagnostics.Log(
                    "Thread.debug00",
                    $"[Thread.debug00][CombatEventDispatcher.Dispatch] tid={Thread.CurrentThread.ManagedThreadId} isMain={UnityMainThreadGuard.IsMainThread()} flagId={flagId} ctxCount={contexts.Count}",
                    null);
                UnityThread.AssertMainThread("CombatEventDispatcher.Dispatch");

                try
                {
                    DispatchInternal(flagId, contexts);
                }
                finally
                {
                    for (int i = 0; i < contexts.Count; i++)
                    {
                        contexts[i]?.Release();
                    }

                    contexts.Clear();
                }
            }

            if (mainThreadInvoker != null)
            {
                mainThreadInvoker.Run(Invoke);
            }
            else
            {
                Invoke();
            }
        }

        private void DispatchWithStagger(string flagId, List<CombatEventContext> contexts, float staggerStepSeconds)
        {
            if (contexts == null || contexts.Count == 0)
            {
                return;
            }

            if (mainThreadInvoker == null || staggerStepSeconds <= 0f || contexts.Count <= 1)
            {
                Dispatch(flagId, contexts);
                return;
            }

            var stagedContexts = new CombatEventContext[contexts.Count];
            contexts.CopyTo(stagedContexts, 0);
            contexts.Clear();

            _ = Task.Run(async () =>
            {
                try
                {
                    for (int i = 0; i < stagedContexts.Length; i++)
                    {
                        var context = stagedContexts[i];
                        stagedContexts[i] = null;
                        if (context == null)
                        {
                            continue;
                        }

                        var singleDispatch = new List<CombatEventContext>(1) { context };
                        Dispatch(flagId, singleDispatch);

                        if (i < stagedContexts.Length - 1)
                        {
                            try
                            {
                                await Task.Delay(TimeSpan.FromSeconds(staggerStepSeconds));
                            }
                            catch (TaskCanceledException)
                            {
                                break;
                            }
                            catch (Exception ex)
                            {
                                BattleLogger.Warn(LogTag, $"Stagger dispatch interrupted: {ex.Message}");
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    BattleLogger.Warn(LogTag, $"Stagger dispatch failed for '{flagId}': {ex}");
                }
            });
        }

        private void DispatchInternal(string flagId, List<CombatEventContext> contexts)
        {
            UnityThread.AssertMainThread("CombatEventDispatcher.DispatchInternal");

            ICombatEventListener[] snapshot;
            lock (listenerGate)
            {
                if (listeners.Count == 0)
                {
                    return;
                }

                snapshot = new ICombatEventListener[listeners.Count];
                listeners.CopyTo(snapshot, 0);
            }

            for (int i = 0; i < snapshot.Length; i++)
            {
                var listener = snapshot[i];
                if (listener == null)
                {
                    continue;
                }

                for (int j = 0; j < contexts.Count; j++)
                {
                    var context = contexts[j];
                    if (context == null)
                    {
                        continue;
                    }

                    try
                    {
                        listener.OnCombatEventRaised(flagId, context);
                    }
                    catch (Exception ex)
                    {
                        BattleLogger.Warn(LogTag, $"Listener '{listener.GetType().Name}' threw while handling '{flagId}': {ex}");
                    }
                }
            }
        }

        private static bool TryResolveFlag(string groupId, out string flagId)
        {
            for (int i = 0; i < GroupFlags.Length; i++)
            {
                var candidate = GroupFlags[i];
                if (IsMatch(groupId, candidate))
                {
                    flagId = candidate;
                    return true;
                }
            }

            flagId = null;
            return false;
        }

        private static bool IsMatch(string source, string candidate)
        {
            if (string.Equals(source, candidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (source.StartsWith(candidate, StringComparison.OrdinalIgnoreCase))
            {
                if (source.Length == candidate.Length)
                {
                    return true;
                }

                char separator = source[candidate.Length];
                return separator == '/' || separator == ':' || separator == '-';
            }

            return false;
        }
    }
}
