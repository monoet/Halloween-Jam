using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using BattleV2.AnimationSystem;
using BattleV2.AnimationSystem.Catalog;
using BattleV2.AnimationSystem.Execution;
using BattleV2.AnimationSystem.Execution.Routers;
using BattleV2.AnimationSystem.Execution.Runtime;
using BattleV2.AnimationSystem.Execution.Runtime.Recipes;
using BattleV2.AnimationSystem.Runtime.Internal;
using BattleV2.AnimationSystem.Timelines;
using BattleV2.Core;
using UnityEngine;
using BattleV2.Orchestration.Runtime;
using BattleV2.AnimationSystem.Strategies;

namespace BattleV2.AnimationSystem.Runtime
{
    /// <summary>
    /// Adapter defined in JRPG Animation System LOCKED (sección 5).
    /// Coordinates timeline playback (sequencer + wrapper + routers) without touching BattleManagerV2.
    /// </summary>
    public sealed class NewAnimOrchestratorAdapter : IAnimationOrchestrator, IDisposable
    {
        private const string PhaseLogScope = "AnimAdapter/Phase";
        private const string RecipeLogScope = "AnimAdapter/Recipe";

        private readonly TimelineRuntimeBuilder runtimeBuilder;
        private readonly ActionSequencerDriver sequencerDriver;
        private readonly ActionTimelineCatalog timelineCatalog;
        private readonly AnimatorWrapperResolver wrapperResolver;
        private readonly AnimationClipResolver clipResolver;
        private readonly AnimationRouterBundle routerBundle;
        private readonly StepScheduler stepScheduler;
        private readonly IAnimationEventBus eventBus;
        private readonly ITimedHitService timedHitService;
        private readonly AnimatorRegistry registry;
        private readonly ActionRecipeCatalog recipeCatalog;
        private readonly Dictionary<BattlePhase, IPhaseStrategy> phaseStrategies;
        private readonly List<IRecipeExecutor> recipeExecutors;
        private readonly IOrchestratorSessionController sessionController;
        private readonly Dictionary<CombatantState, IPlaybackSession> activeSessions = new Dictionary<CombatantState, IPlaybackSession>();
        private readonly Dictionary<CombatantState, IAnimationWrapper> legacyAdapters = new Dictionary<CombatantState, IAnimationWrapper>();
        private readonly BattlePacingSettings pacingSettings;
        private static readonly HashSet<string> AuxiliaryRecipeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            PilotActionRecipes.RunUpId,
            PilotActionRecipes.RunBackId,
            PilotActionRecipes.IdleId,
            PilotActionRecipes.TurnIntroId
        };

        private bool disposed;

        public NewAnimOrchestratorAdapter(
            TimelineRuntimeBuilder runtimeBuilder,
            ActionSequencerDriver sequencerDriver,
            ActionTimelineCatalog timelineCatalog,
            IActionLockManager lockManager,
            IAnimationEventBus eventBus,
            ITimedHitService timedHitService,
            AnimatorWrapperResolver wrapperResolver,
            AnimationClipResolver clipResolver,
            AnimationRouterBundle routerBundle,
            StepScheduler stepScheduler,
            ActionRecipeCatalog recipeCatalog,
            AnimatorRegistry registry,
            IReadOnlyDictionary<BattlePhase, IPhaseStrategy> phaseStrategies = null,
            IEnumerable<IRecipeExecutor> recipeExecutors = null,
            IOrchestratorSessionController sessionController = null,
            BattlePacingSettings pacingSettings = null)
        {
            if (runtimeBuilder == null) throw new ArgumentNullException(nameof(runtimeBuilder));
            if (sequencerDriver == null) throw new ArgumentNullException(nameof(sequencerDriver));
            if (timelineCatalog == null) throw new ArgumentNullException(nameof(timelineCatalog));
            if (lockManager == null) throw new ArgumentNullException(nameof(lockManager));
            if (eventBus == null) throw new ArgumentNullException(nameof(eventBus));
            if (timedHitService == null) throw new ArgumentNullException(nameof(timedHitService));
            if (wrapperResolver == null) throw new ArgumentNullException(nameof(wrapperResolver));
            if (clipResolver == null) throw new ArgumentNullException(nameof(clipResolver));
            if (routerBundle == null) throw new ArgumentNullException(nameof(routerBundle));
            if (stepScheduler == null) throw new ArgumentNullException(nameof(stepScheduler));
            if (recipeCatalog == null) throw new ArgumentNullException(nameof(recipeCatalog));

            this.runtimeBuilder = runtimeBuilder;
            this.sequencerDriver = sequencerDriver;
            this.timelineCatalog = timelineCatalog;
            this.eventBus = eventBus;
            this.timedHitService = timedHitService;
            this.wrapperResolver = wrapperResolver;
            this.clipResolver = clipResolver;
            this.routerBundle = routerBundle;
            this.stepScheduler = stepScheduler;
            this.recipeCatalog = recipeCatalog;
            this.registry = registry ?? AnimatorRegistry.Instance;
            this.phaseStrategies = phaseStrategies != null
                ? new Dictionary<BattlePhase, IPhaseStrategy>(phaseStrategies)
                : new Dictionary<BattlePhase, IPhaseStrategy>();
            this.recipeExecutors = recipeExecutors != null
                ? new List<IRecipeExecutor>(recipeExecutors)
                : new List<IRecipeExecutor>();
            this.sessionController = sessionController ?? new OrchestratorSessionController();
            this.pacingSettings = pacingSettings;

            // Ensure noop executor is available for synthetic recipes (e.g., auto run_back fallback).
            stepScheduler.RegisterExecutor(new NoOpStepExecutor());
        }

        public BattlePhase CurrentPhase => sessionController.GetPhase(AnimationContext.Default);

        public BattlePhase GetCurrentPhase(AnimationContext context)
        {
            var normalized = NormalizeContext(context);
            return sessionController.GetPhase(normalized);
        }

        public void EnterPhase(BattlePhase phase, AnimationContext context)
        {
            var normalized = NormalizeContext(context);
            var previousPhase = sessionController.GetPhase(normalized);

            if (!phaseStrategies.TryGetValue(previousPhase, out var exitStrategy))
            {
                exitStrategy = null;
            }

            if (!phaseStrategies.TryGetValue(phase, out var enterStrategy))
            {
                enterStrategy = null;
            }

            var strategyContext = new StrategyContext(PhaseLogScope, normalized, orchestrator: this);

            exitStrategy?.OnExit(strategyContext.WithPhase(previousPhase));
            sessionController.SetPhase(phase, normalized);
            enterStrategy?.OnEnter(strategyContext.WithPhase(phase));
        }

        public AmbientHandle StartAmbient(AmbientSpec spec, AnimationContext context)
        {
            if (spec == null)
            {
                BattleLogger.Warn("AnimAdapter", "StartAmbient called with null spec.");
                return AmbientHandle.Invalid;
            }

            var normalized = NormalizeContext(context);
            return sessionController.StartAmbient(spec, normalized);
        }

        public void StopAmbient(AmbientHandle handle, AnimationContext context)
        {
            if (!handle.IsValid)
            {
                return;
            }

            sessionController.StopAmbient(handle);
        }

        public Task PlayRecipeAsync(string recipeId, AnimationContext context)
        {
            if (string.IsNullOrWhiteSpace(recipeId))
            {
                return Task.CompletedTask;
            }

            var normalized = NormalizeContext(context);
            var isRouter = !string.IsNullOrWhiteSpace(recipeId) &&
                recipeId.StartsWith("router:", StringComparison.OrdinalIgnoreCase);

            if (!isRouter && !recipeCatalog.TryResolveRecipe(recipeId, out _))
            {
                BattleLogger.Warn("AnimOrchestrator", $"Skipped recipe {recipeId} (not found in catalog).");
                return Task.CompletedTask;
            }

            var strategyContext = new StrategyContext(RecipeLogScope, normalized, orchestrator: this).WithRecipe(recipeId);

            for (int i = 0; i < recipeExecutors.Count; i++)
            {
                var executor = recipeExecutors[i];
                if (executor == null)
                {
                    continue;
                }

                if (!executor.CanExecute(recipeId, strategyContext))
                {
                    continue;
                }

                return executor.ExecuteAsync(recipeId, strategyContext);
            }

            BattleLogger.Warn("AnimAdapter", $"No recipe executor registered for '{recipeId}' (context='{normalized.SessionId}').");
            return Task.CompletedTask;
        }

        [Obsolete("Use EnterPhase + StartAmbient instead of PlayIntroAsync.")]
        public Task PlayIntroAsync()
        {
            EnterPhase(BattlePhase.Intro, AnimationContext.Default);
            _ = StartAmbient(AmbientSpec.IntroDefault(), AnimationContext.Default);
            return Task.CompletedTask;
        }

        [Obsolete("Use EnterPhase + StartAmbient instead of PlayLoopAmbientAsync.")]
        public Task PlayLoopAmbientAsync()
        {
            EnterPhase(BattlePhase.Loop, AnimationContext.Default);
            _ = StartAmbient(AmbientSpec.DefaultLoop(), AnimationContext.Default);
            return Task.CompletedTask;
        }

        [Obsolete("Use EnterPhase + PlayRecipeAsync instead of PlayCinematicAsync.")]
        public Task PlayCinematicAsync(string recipeId)
        {
            EnterPhase(BattlePhase.Cinematic, AnimationContext.Default);
            return PlayRecipeAsync(recipeId, AnimationContext.Default);
        }

        public async Task PlayAsync(AnimationRequest request, CancellationToken cancellationToken = default)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(NewAnimOrchestratorAdapter));
            }

            // AnimationRequest is likely a struct; it cannot be null. Validate required fields instead.
            if (request.Actor == null)
            {
                BattleLogger.Warn("AnimAdapter", "AnimationRequest missing actor. Ignoring playback.");
                return;
            }

            var selection = request.Selection;
            var action = selection.Action;
            var overrideRecipeId = !string.IsNullOrWhiteSpace(request.RecipeOverride)
                ? request.RecipeOverride
                : selection.AnimationRecipeId;
            var actionId = !string.IsNullOrWhiteSpace(overrideRecipeId)
                ? overrideRecipeId
                : action?.id;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var actorLabel = request.Actor != null ? request.Actor.GetInstanceID().ToString() : "(null)";
            Debug.Log($"[AnimAdapter] PlayAsync request for action '{actionId ?? "(null)"}' (actorId={actorLabel})");
#else
            var actorLabel = request.Actor != null ? request.Actor.GetInstanceID().ToString() : "(null)";
#endif

            if (string.IsNullOrWhiteSpace(actionId))
            {
                BattleLogger.Warn("AnimAdapter", "PlayAsync request missing action and recipe id. Aborting.");
                return;
            }

            ActionRecipe recipe = null;
            var hasRecipe = !string.IsNullOrWhiteSpace(actionId) &&
                            ((recipeCatalog != null && recipeCatalog.TryGet(actionId, out recipe)) ||
                             stepScheduler.TryGetRecipe(actionId, out recipe));

            ActionTimeline timeline = null;
            if (timelineCatalog != null && action != null && !string.IsNullOrWhiteSpace(action.id))
            {
                timeline = timelineCatalog.GetTimelineOrDefault(action.id);
            }

            if (!hasRecipe && timeline == null)
            {
                BattleLogger.Warn("AnimAdapter", $"No timeline or recipe registered for action '{actionId}'.");
                return;
            }

            IAnimationWrapper wrapper = null;
            if (registry != null)
            {
                registry.TryGetWrapper(request.Actor, out wrapper);
            }

            if (wrapper == null && wrapperResolver != null)
            {
                var legacyWrapper = wrapperResolver.Resolve(request.Actor);
                if (legacyWrapper != null)
                {
                    if (!legacyAdapters.TryGetValue(request.Actor, out wrapper))
                    {
                        wrapper = registry.ResolveLegacyWrapper(request.Actor, legacyWrapper);
                        if (wrapper != null)
                        {
                            legacyAdapters[request.Actor] = wrapper;
                        }
                    }
                }
            }

            if (wrapper == null)
            {
                BattleLogger.Warn("AnimAdapter", $"No AnimatorWrapper binding configured for actorId '{actorLabel}'.");
                return;
            }

            var bindingResolver = wrapper as IAnimationBindingResolver;
            if (hasRecipe && bindingResolver == null)
            {
                BattleLogger.Warn("AnimAdapter", $"Wrapper for actorId '{actorLabel}' does not expose binding resolver. Falling back to legacy timeline.");
                hasRecipe = false;
            }

            if (hasRecipe && recipe == null)
            {
                // Defensive: Try pull from scheduler registry.
                stepScheduler.TryGetRecipe(actionId, out recipe);
            }

            if (hasRecipe && recipe == null)
            {
                BattleLogger.Warn("AnimAdapter", $"Recipe lookup failed for action '{actionId}'. Falling back to timeline.");
                hasRecipe = false;
            }

            if (!hasRecipe && timeline == null)
            {
                BattleLogger.Warn("AnimAdapter", $"No timeline available for action '{actionId}'.");
                return;
            }

            if (activeSessions.TryGetValue(request.Actor, out var previousSession))
            {
                try
                {
                    await previousSession.CancelAsync();
                }
                catch (OperationCanceledException)
                {
                    // Expected when the previous session completes via cancellation.
                }

                if (activeSessions.TryGetValue(request.Actor, out var current) && ReferenceEquals(current, previousSession))
                {
                    activeSessions.Remove(request.Actor);
                }

                if (!previousSession.IsDisposed)
                {
                    previousSession.Dispose();
                }
            }

            var actor = request.Actor;

            if (hasRecipe && actor != null && actor.IsEnemy && AuxiliaryRecipeIds.Contains(recipe.Id))
            {
                BattleLogger.Warn("AnimAdapter", $"Enemy actor '{actor.name}' attempted to play auxiliary recipe '{recipe.Id}'. Skipping execution.");
                return;
            }

            IPlaybackSession session;
            if (hasRecipe)
            {
                var isEnemy = actor != null && actor.IsEnemy;
                var useLifecycle = actor != null && actor.IsPlayer && ShouldUseLifecycle(recipe);
                session = new RecipePlaybackSession(
                    request,
                    timeline,
                    wrapper,
                    bindingResolver,
                    routerBundle,
                    eventBus,
                    timedHitService,
                    stepScheduler,
                    recipe,
                    useLifecycle,
                    pacingSettings,
                    isEnemy);
            }
            else
            {
                var sequencer = runtimeBuilder.Create(request, timeline);
                var timelineSession = new AnimationSequenceSession(
                    request,
                    timeline,
                    sequencer,
                    wrapper,
                    clipResolver,
                    routerBundle,
                    stepScheduler,
                    recipeCatalog,
                    eventBus,
                    timedHitService);
                session = new TimelinePlaybackSession(timelineSession);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (timeline != null)
                {
                    Debug.Log($"[AnimAdapter] Timeline '{timeline.ActionId}' resolved for action '{actionId}'.");
                }
#endif
            }

            activeSessions[request.Actor] = session;
            try
            {
                await session.RunAsync(sequencerDriver, cancellationToken);
            }
            finally
            {
                if (activeSessions.TryGetValue(request.Actor, out var current) && ReferenceEquals(current, session))
                {
                    activeSessions.Remove(request.Actor);
                }

                session.Dispose();
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            routerBundle.Dispose();
            wrapperResolver.Dispose();
            legacyAdapters.Clear();
        }

        private static AnimationContext NormalizeContext(AnimationContext context)
        {
            if (string.IsNullOrWhiteSpace(context.SessionId))
            {
                return AnimationContext.Default;
            }

            return context;
        }


        private interface IPlaybackSession : IDisposable
        {
            Task RunAsync(ActionSequencerDriver driver, CancellationToken cancellationToken);
            Task CancelAsync();
            bool IsDisposed { get; }
        }

        private sealed class TimelinePlaybackSession : IPlaybackSession
        {
            private readonly AnimationSequenceSession session;
            private bool disposed;

            public TimelinePlaybackSession(AnimationSequenceSession session)
            {
                if (session == null) throw new ArgumentNullException(nameof(session));
                this.session = session;
            }

            public Task RunAsync(ActionSequencerDriver driver, CancellationToken cancellationToken)
            {
                if (disposed) throw new ObjectDisposedException(nameof(TimelinePlaybackSession));
                return session.RunAsync(driver, cancellationToken);
            }

            public Task CancelAsync()
            {
                if (disposed) return Task.CompletedTask;
                return session.CancelAsync();
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                session.Dispose();
            }

            public bool IsDisposed => disposed;
        }

        private bool ShouldUseLifecycle(ActionRecipe recipe)
        {
            if (recipe == null || string.IsNullOrWhiteSpace(recipe.Id))
            {
                return false;
            }

            return !AuxiliaryRecipeIds.Contains(recipe.Id);
        }

        private sealed class RecipePlaybackSession : IPlaybackSession
        {
            private readonly AnimationRequest request;
            private readonly ActionTimeline timeline;
            private readonly IAnimationWrapper wrapper;
            private readonly IAnimationBindingResolver bindingResolver;
            private readonly AnimationRouterBundle routerBundle;
            private readonly IAnimationEventBus eventBus;
            private readonly ITimedHitService timedHitService;
            private readonly StepScheduler scheduler;
            private readonly ActionRecipe recipe;
            private readonly BattlePacingSettings pacingSettings;
            private readonly bool useLifecycle;
            private readonly bool isEnemyActor;
            private const string RunUpGroupId = "run_up";
            private const string RunUpTargetGroupId = "run_up_target";
            private const string RunBackGroupId = "run_back";
            private const bool EnableAutoReturnEnvelope = true;

            private CancellationTokenSource linkedCts;
            private Task executionTask;
            private bool actorRegistered;
            private bool disposed;

            public RecipePlaybackSession(
                AnimationRequest request,
                ActionTimeline timeline,
                IAnimationWrapper wrapper,
                IAnimationBindingResolver bindingResolver,
                AnimationRouterBundle routerBundle,
                IAnimationEventBus eventBus,
                ITimedHitService timedHitService,
                StepScheduler scheduler,
                ActionRecipe recipe,
                bool useLifecycle,
                BattlePacingSettings pacingSettings,
                bool isEnemyActor)
            {
                // AnimationRequest is a struct; it won't be null.
                if (wrapper == null) throw new ArgumentNullException(nameof(wrapper));
                if (bindingResolver == null) throw new ArgumentNullException(nameof(bindingResolver));
                if (routerBundle == null) throw new ArgumentNullException(nameof(routerBundle));
                if (eventBus == null) throw new ArgumentNullException(nameof(eventBus));
                if (timedHitService == null) throw new ArgumentNullException(nameof(timedHitService));
                if (scheduler == null) throw new ArgumentNullException(nameof(scheduler));
                if (recipe == null) throw new ArgumentNullException(nameof(recipe));

                this.request = request;
                this.timeline = timeline;
                this.wrapper = wrapper;
                this.bindingResolver = bindingResolver;
                this.routerBundle = routerBundle;
                this.eventBus = eventBus;
                this.timedHitService = timedHitService;
                this.scheduler = scheduler;
                this.recipe = recipe;
                this.pacingSettings = pacingSettings;
                this.isEnemyActor = isEnemyActor;
                this.useLifecycle = useLifecycle;
            }

            public async Task RunAsync(ActionSequencerDriver driver, CancellationToken cancellationToken)
            {
                if (disposed) throw new ObjectDisposedException(nameof(RecipePlaybackSession));
                if (linkedCts != null)
                {
                    throw new InvalidOperationException("Playback session already running.");
                }

                routerBundle.RegisterActor(request.Actor);
                actorRegistered = true;

                linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var context = new StepSchedulerContext(request, timeline, wrapper, bindingResolver, routerBundle, eventBus, timedHitService, skipResetToFallback: false, gate: new BattleV2.AnimationSystem.Execution.Runtime.Core.ExternalBarrierGate());

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[AnimAdapter] Executing recipe '{recipe.Id}' for action '{request.Selection.Action?.id ?? "(null)"}'.");
#endif

                var plan = BuildExecutionPlan(recipe);

                try
                {
                    if (useLifecycle)
                    {
                        await BattlePacingUtility.DelayTrackedAsync(pacingSettings != null ? pacingSettings.playerPreDelay : 0f, "PlayerPreDelay", request.Actor, linkedCts.Token, waitForIdle: false);
                    }

                    executionTask = ExecutePlanAsync(plan, context, linkedCts.Token);

                    await executionTask;
                    await StepSchedulerIdleUtility.WaitUntilActorIdleAsync(request.Actor, linkedCts.Token);

                    if (useLifecycle)
                    {
                        await BattlePacingUtility.DelayTrackedAsync(pacingSettings != null ? pacingSettings.playerPostDelay : 0f, "PlayerPostDelay", request.Actor, linkedCts.Token);
                    }
                    else if (isEnemyActor)
                    {
                        await BattlePacingUtility.DelayTrackedAsync(pacingSettings != null ? pacingSettings.enemyTurnGap : 0f, "EnemyTurnGap", request.Actor, linkedCts.Token);
                    }
                }
                catch (OperationCanceledException) when ((linkedCts != null && linkedCts.IsCancellationRequested) || cancellationToken.IsCancellationRequested)
                {
                    // Expected on cancellation.
                }
                finally
                {
                    if (linkedCts != null)
                    {
                        linkedCts.Dispose();
                        linkedCts = null;
                    }
                    executionTask = null;
                    if (actorRegistered)
                    {
                        routerBundle.UnregisterActor(request.Actor);
                        actorRegistered = false;
                    }
                }
            }

            private sealed class ExecutionPlan
            {
                public readonly List<ActionRecipe> Main = new List<ActionRecipe>();
                public readonly List<ActionRecipe> Finally = new List<ActionRecipe>();
                public bool HasInjectedMoveToTarget;
            }

            private ExecutionPlan BuildExecutionPlan(ActionRecipe baseRecipe)
            {
                var plan = new ExecutionPlan();
                if (baseRecipe != null && !baseRecipe.IsEmpty)
                {
                    // NOTE: this is a feature flag (not just logging). Keep separate from AP logging.
                    if (BattleV2.Diagnostics.BattleDebug.IsEnabled("APF") &&
                        string.Equals(baseRecipe.Id, "basic_attack", StringComparison.OrdinalIgnoreCase) &&
                        TryResolveMoveToTarget(out var moveToTarget))
                    {
                        plan.Main.Add(moveToTarget);
                        plan.HasInjectedMoveToTarget = true;
                    }
                    plan.Main.Add(baseRecipe);
                }

                if (EnableAutoReturnEnvelope && baseRecipe != null && !ContainsRunBack(baseRecipe) && TryResolveRunBack(out var runBack))
                {
                    plan.Finally.Add(runBack);
                }

                return plan;
            }

            private async Task ExecutePlanAsync(ExecutionPlan plan, StepSchedulerContext context, CancellationToken mainToken)
            {
                try
                {
                    for (int i = 0; i < plan.Main.Count; i++)
                    {
                        await ExecutePlanNodeAsync(plan, plan.Main[i], context, mainToken);
                    }
                }
                finally
                {
                    if (plan.Finally.Count > 0)
                    {
                        for (int i = 0; i < plan.Finally.Count; i++)
                        {
                            var recipeToRun = plan.Finally[i];
                            if (recipeToRun == null || recipeToRun.IsEmpty)
                            {
                                continue;
                            }

                            try
                            {
                                // Ejecuta retorno sin lifecycle para evitar run_up implícitos en fases.
                                var finalContext = context.WithSkipReset(true);
                                await scheduler.ExecuteAsync(recipeToRun, finalContext, CancellationToken.None);
                            }
                            catch (OperationCanceledException)
                            {
                                // swallow: return attempt was cancelled
                            }
                            catch (Exception ex)
                            {
                                BattleLogger.Warn("AnimAdapter", $"Finally recipe '{recipeToRun.Id}' failed: {ex.Message}");
                            }
                        }
                    }
                }
            }

            private Task ExecutePlanNodeAsync(ExecutionPlan plan, ActionRecipe actionRecipe, StepSchedulerContext context, CancellationToken token)
            {
                if (actionRecipe == null || actionRecipe.IsEmpty)
                {
                    return Task.CompletedTask;
                }

                // Chunk 4.5: injected locomotion-only recipes must NOT run with lifecycle,
                // otherwise they will get auto run_up/run_back around them (chaos).
                if (string.Equals(actionRecipe.Id, "move_to_target", StringComparison.OrdinalIgnoreCase))
                {
                    return scheduler.ExecuteAsync(actionRecipe, context.WithSkipReset(true), token);
                }

                // Chunk 4.5: if we injected move_to_target, we must not run the payload recipe with lifecycle,
                // otherwise lifecycle pre-action will run run_up (spotlight) and yank the actor away from target.
                if (plan != null && plan.HasInjectedMoveToTarget && string.Equals(actionRecipe.Id, "basic_attack", StringComparison.OrdinalIgnoreCase))
                {
                    return scheduler.ExecuteAsync(actionRecipe, context.WithSkipReset(true), token);
                }

                return ExecuteSingleAsync(actionRecipe, context, token);
            }

            private Task ExecuteSingleAsync(ActionRecipe actionRecipe, StepSchedulerContext context, CancellationToken token)
            {
                return useLifecycle
                    ? scheduler.ExecuteLifecycleAsync(actionRecipe, context, token)
                    : scheduler.ExecuteAsync(actionRecipe, context, token);
            }

            private bool ContainsMovementWithoutReturn(ActionRecipe actionRecipe)
            {
                if (actionRecipe == null || actionRecipe.Groups == null || actionRecipe.Groups.Count == 0)
                {
                    return false;
                }

                bool hasApproach = actionRecipe.Groups.Any(g =>
                    MatchesGroupId(g, RunUpGroupId) || MatchesGroupId(g, RunUpTargetGroupId));
                if (!hasApproach)
                {
                    return false;
                }

                bool hasReturn = actionRecipe.Groups.Any(g => MatchesGroupId(g, RunBackGroupId));
                return !hasReturn;
            }

            private bool ContainsRunBack(ActionRecipe actionRecipe)
            {
                if (actionRecipe == null || actionRecipe.Groups == null || actionRecipe.Groups.Count == 0)
                {
                    return false;
                }

                return actionRecipe.Groups.Any(g => MatchesGroupId(g, RunBackGroupId));
            }

            private bool TryResolveRunBack(out ActionRecipe runBackRecipe)
            {
                runBackRecipe = null;
                if (scheduler.TryGetRecipe(RunBackGroupId, out var registered) && registered != null && !registered.IsEmpty)
                {
                    runBackRecipe = registered;
                    return true;
                }

                // Fallback sintético: si no hay asset registrado, emitimos un recipe mínimo (noop) para que los observers
                // reciban RunBack y ejecuten el tween de retorno.
                var steps = new List<ActionStep>
                {
                    new ActionStep(
                        executorId: NoOpStepExecutor.ExecutorId,
                        bindingId: null,
                        parameters: new ActionStepParameters(null),
                        conflictPolicy: StepConflictPolicy.WaitForCompletion,
                        id: "noop")
                };
                var group = new ActionStepGroup(
                    RunBackGroupId,
                    steps,
                    StepGroupExecutionMode.Sequential,
                    StepGroupJoinPolicy.Any);
                runBackRecipe = new ActionRecipe(RunBackGroupId, new[] { group });
                return true;
            }

            private bool TryResolveMoveToTarget(out ActionRecipe moveToTargetRecipe)
            {
                const string moveToTargetId = "move_to_target";
                moveToTargetRecipe = null;

                if (scheduler.TryGetRecipe(moveToTargetId, out var registered) && registered != null && !registered.IsEmpty)
                {
                    moveToTargetRecipe = registered;
                    return true;
                }

                var steps = new List<ActionStep>
                {
                    new ActionStep(
                        executorId: NoOpStepExecutor.ExecutorId,
                        bindingId: null,
                        parameters: new ActionStepParameters(null),
                        conflictPolicy: StepConflictPolicy.WaitForCompletion,
                        id: "noop")
                };
                var group = new ActionStepGroup(
                    moveToTargetId,
                    steps,
                    StepGroupExecutionMode.Sequential,
                    StepGroupJoinPolicy.Any);
                moveToTargetRecipe = new ActionRecipe(moveToTargetId, new[] { group });
                return true;
            }

            private static bool MatchesGroupId(ActionStepGroup group, string id)
            {
                if (group == null || string.IsNullOrWhiteSpace(group.Id) || string.IsNullOrWhiteSpace(id))
                {
                    return false;
                }

                return string.Equals(group.Id, id, StringComparison.OrdinalIgnoreCase);
            }

            public async Task CancelAsync()
            {
                if (disposed) return;

                var cts = linkedCts;
                if (cts == null)
                {
                    return;
                }

                if (!cts.IsCancellationRequested)
                {
                    cts.Cancel();
                }

                if (executionTask != null)
                {
                    try
                    {
                        await executionTask;
                    }
                    catch (OperationCanceledException)
                    {
                        // swallow expected cancellation
                    }
                    catch (Exception ex)
                    {
                        BattleLogger.Warn("AnimAdapter", $"Recipe playback cancelled with exception: {ex}");
                    }
                }
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;

                if (linkedCts != null)
                {
                    linkedCts.Dispose();
                    linkedCts = null;
                }
                executionTask = null;

                if (actorRegistered)
                {
                    routerBundle.UnregisterActor(request.Actor);
                    actorRegistered = false;
                }
            }

            public bool IsDisposed => disposed;
        }
    }
}
