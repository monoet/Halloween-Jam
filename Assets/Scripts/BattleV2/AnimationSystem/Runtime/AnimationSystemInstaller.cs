using System;
using System.Collections.Generic;
using BattleV2.AnimationSystem.Catalog;
using BattleV2.AnimationSystem.Execution;
using BattleV2.AnimationSystem.Execution.Runtime;
using BattleV2.AnimationSystem.Execution.Runtime.Executors;
using BattleV2.AnimationSystem.Execution.Runtime.Recipes;
using BattleV2.AnimationSystem.Execution.Runtime.CombatEvents;
using BattleV2.AnimationSystem.Execution.Runtime.Telemetry;
using BattleV2.AnimationSystem.Execution.Routers;
using BattleV2.AnimationSystem.Execution.Runtime.Tweens;
using BattleV2.AnimationSystem.Motion;
using BattleV2.AnimationSystem.Runtime.Internal;
using BattleV2.AnimationSystem.Timelines;
using BattleV2.Core;
using BattleV2.Orchestration.Runtime;
using BattleV2.Execution.TimedHits;
using BattleV2.AnimationSystem.Strategies;
using HalloweenJam.Combat.Animations.StepScheduler;
using UnityEngine;

namespace BattleV2.AnimationSystem.Runtime
{
    /// <summary>
    /// Scene-level composition root for the JRPG animation system.
    /// Instantiates core services and exposes an orchestrator implementation.
    /// </summary>
    public sealed class AnimationSystemInstaller : MonoBehaviour, IOrchestratorDiagnosticsProvider
    {
        public static AnimationSystemInstaller Current { get; private set; }

        [Header("Dependencies")]
        [SerializeField] private ActionSequencerDriver sequencerDriver;
        [SerializeField] private ActionTimelineCatalog timelineCatalog;

        [Header("Optional Services")]
        [SerializeField] private CombatClock combatClock;
        [SerializeField] private bool autoPopulateActorBindings = true;
        [SerializeField] private AnimationClip defaultFallbackClip;
        [SerializeField] private AnimationActorBinding[] actorBindings;
        [SerializeField] private AnimationClipBinding[] clipBindings;
        [SerializeField] private UnityEngine.Object vfxServiceSource;
        [SerializeField] private UnityEngine.Object sfxServiceSource;
        [SerializeField] private UnityEngine.Object cameraServiceSource;
        [SerializeField] private UnityEngine.Object uiServiceSource;
        [Header("Pacing")]
        [SerializeField] private BattlePacingSettings pacingSettings;
        [Header("Binding Sources")]
        [SerializeField] private bool autoScanBindings = true;
        [Header("Timed Hit Settings")]
        [SerializeField] private TimedHitToleranceProfileAsset toleranceProfileAsset;
        [SerializeField] private Ks1TimedHitRunner ks1TimedHitRunner;
        [SerializeField] private BasicTimedHitRunner basicTimedHitRunner;
        [Tooltip("How long (in seconds) to keep input in the buffer before pruning. Must be longer than the longest Timed Hit window.")]
        [SerializeField, Min(0.5f)] private double inputBufferRetention = 2.0d;

        [Header("Recipe Defaults")]
        [Tooltip("Register the built-in PilotActionRecipes (turn_intro, run_up, etc.) for legacy scenes.")]
        [SerializeField] private bool includePilotRecipes = true;
        [Header("Step Scheduler Recipes")]
        [SerializeField] private StepRecipeAsset[] stepRecipeAssets = Array.Empty<StepRecipeAsset>();

        private AnimationEventBus eventBus;
        private ActionLockManager lockManager;
        private TimelineCompiler timelineCompiler;
        private TimelineRuntimeBuilder runtimeBuilder;
        private TimedInputBuffer timedInputBuffer;
        private DefaultTimedHitToleranceProfile toleranceProfile;
        private TimedHitService timedHitService;
        private AnimatorWrapperResolver wrapperResolver;
        private AnimationClipResolver clipResolver;
        private AnimationRouterBundle routerBundle;
        private NewAnimOrchestratorAdapter orchestrator;
        private StepScheduler stepScheduler;
        private StepSchedulerHooks schedulerHooks;
        private StepSchedulerMetricsObserver schedulerMetrics;
        private MotionService motionService;
        private CombatEventDispatcher combatEventDispatcher;
        private ActionRecipeCatalog recipeCatalog;
        private IReadOnlyDictionary<BattlePhase, IPhaseStrategy> phaseStrategyMap;
        private IOrchestratorSessionController sessionController;
        private IMainThreadInvoker mainThreadInvoker;
        private ITweenBindingResolver tweenBindingResolver;
        private ITweenBridge tweenBridge;
        private readonly List<string> bindingProviderSummaries = new List<string>();
        private readonly List<string> bindingProviderIssues = new List<string>();

        public IAnimationEventBus EventBus => eventBus;
        public ICombatClock Clock => combatClock;
        public IActionLockManager LockManager => lockManager;
        public ITimedHitService TimedHitService => timedHitService;
        public IAnimationOrchestrator Orchestrator => orchestrator;
        public AnimationClipResolver ClipResolver => clipResolver;
        public StepScheduler StepScheduler => stepScheduler;
        public StepSchedulerHooks SchedulerHooks => schedulerHooks;
        public StepSchedulerMetricsObserver SchedulerMetrics => schedulerMetrics;
        public MotionService MotionService => motionService;
        public ActionRecipeCatalog RecipeCatalog => recipeCatalog;
        public CombatEventDispatcher CombatEvents => combatEventDispatcher;
        public BattlePacingSettings PacingSettings => pacingSettings;
        public static BattlePacingSettings ActivePacing { get; private set; }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public OrchestratorDiagnosticsSnapshot GetDiagnostics()
        {
            var routerInfo = routerBundle != null ? routerBundle.GetDiagnostics() : RouterDiagnosticsInfo.Empty;
            IReadOnlyDictionary<string, BattlePhase> sessionPhases = sessionController != null
                ? sessionController.SnapshotPhases()
                : new Dictionary<string, BattlePhase>();

            IReadOnlyCollection<string> strategySummaries;
            if (phaseStrategyMap != null)
            {
                var list = new List<string>(phaseStrategyMap.Count);
                foreach (var pair in phaseStrategyMap)
                {
                    var strategyName = pair.Value != null ? pair.Value.GetType().Name : "(null)";
                    list.Add($"{pair.Key}: {strategyName}");
                }

                strategySummaries = list;
            }
            else
            {
                strategySummaries = System.Array.Empty<string>();
            }

            var providersSnapshot = bindingProviderSummaries.Count > 0
                ? bindingProviderSummaries.ToArray()
                : System.Array.Empty<string>();
            var issuesSnapshot = bindingProviderIssues.Count > 0
                ? bindingProviderIssues.ToArray()
                : System.Array.Empty<string>();

            return new OrchestratorDiagnosticsSnapshot(routerInfo, sessionPhases, strategySummaries, providersSnapshot, issuesSnapshot);
        }
#else
        public OrchestratorDiagnosticsSnapshot GetDiagnostics() => default;
#endif

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Debug.LogWarning("[AnimationSystemInstaller] Multiple installers detected. Replacing the active instance.");
            }

            Current = this;
            ActivePacing = pacingSettings;

            combatClock ??= new CombatClock();
            eventBus = new AnimationEventBus();
            lockManager = new ActionLockManager();
            timelineCompiler = new TimelineCompiler();
            runtimeBuilder = new TimelineRuntimeBuilder(timelineCompiler, combatClock, eventBus, lockManager);
            timedInputBuffer = new TimedInputBuffer(combatClock, inputBufferRetention);
            toleranceProfile = DefaultTimedHitToleranceProfile.FromAsset(toleranceProfileAsset);
            timedHitService = new TimedHitService(combatClock, timedInputBuffer, toleranceProfile, eventBus);
            ks1TimedHitRunner ??= FindRunnerInstance<Ks1TimedHitRunner>();
            basicTimedHitRunner ??= FindRunnerInstance<BasicTimedHitRunner>();
            timedHitService.ConfigureRunners(ks1TimedHitRunner, basicTimedHitRunner);
            Debug.Log($"[AnimationSystemInstaller] TimedHitService runners | KS1={ks1TimedHitRunner?.GetType().Name ?? "(null)"} | Basic={basicTimedHitRunner?.GetType().Name ?? "(null)"}", this);

            clipResolver = new AnimationClipResolver(clipBindings);
            wrapperResolver = new AnimatorWrapperResolver(ResolveActorBindings());

            var vfxService = ResolveService<IAnimationVfxService>(vfxServiceSource, "VFX");
            var sfxService = ResolveService<IAnimationSfxService>(sfxServiceSource, "SFX");
            var cameraService = ResolveService<IAnimationCameraService>(cameraServiceSource, "Camera");
            var uiService = ResolveService<IAnimationUiService>(uiServiceSource, "UI");

            routerBundle = new AnimationRouterBundle(eventBus, vfxService, sfxService, cameraService, uiService);
            mainThreadInvoker = MainThreadInvoker.Instance;
            tweenBindingResolver = new DefaultTweenBindingResolver();
            tweenBridge = new DefaultTweenBridge(mainThreadInvoker);
            motionService = new MotionService(mainThreadInvoker);
            stepScheduler = BuildStepScheduler(mainThreadInvoker, tweenBindingResolver, tweenBridge);
            schedulerHooks = new StepSchedulerHooks(stepScheduler);
            combatEventDispatcher = new CombatEventDispatcher(mainThreadInvoker);
            stepScheduler.RegisterObserver(combatEventDispatcher);
            recipeCatalog = BuildRecipeCatalog(stepScheduler);
            RegisterInspectorRecipes();
            phaseStrategyMap = BuildPhaseStrategyMap();
            sessionController = new OrchestratorSessionController();
            orchestrator = new NewAnimOrchestratorAdapter(
                runtimeBuilder,
                sequencerDriver,
                timelineCatalog,
                lockManager,
                eventBus,
                timedHitService,
                wrapperResolver,
                clipResolver,
                routerBundle,
                stepScheduler,
                recipeCatalog,
                AnimatorRegistry.Instance,
                phaseStrategyMap,
                BuildRecipeExecutors(routerBundle),
                sessionController,
                pacingSettings);

            if (sequencerDriver == null)
            {
                Debug.LogError("[AnimationSystemInstaller] Missing ActionSequencerDriver reference.", this);
            }

            if (timelineCatalog == null)
            {
                Debug.LogError("[AnimationSystemInstaller] Missing ActionTimelineCatalog reference.", this);
            }

            Debug.Log($"[INSTALLER] Active Installer | Hash={GetHashCode()} | BusHash={eventBus?.GetHashCode()}", this);
        }

        private void OnDestroy()
        {
            if (stepScheduler != null && combatEventDispatcher != null)
            {
                stepScheduler.UnregisterObserver(combatEventDispatcher);
            }

            schedulerHooks?.Dispose();

            timedHitService?.Dispose();
            routerBundle?.Dispose();
            wrapperResolver?.Dispose();
            AnimatorRegistry.Instance.Clear();
            if (Current == this)
            {
                Current = null;
            }
            if (ActivePacing == pacingSettings)
            {
                ActivePacing = null;
            }
        }

        private T ResolveService<T>(UnityEngine.Object source, string label) where T : class
        {
            if (source == null)
            {
                return null;
            }

            if (source is T typed)
            {
                return typed;
            }

            Debug.LogWarning($"[AnimationSystemInstaller] Assigned {label} service does not implement {typeof(T).Name}. Ignoring reference.", source);
            return null;
        }

        private AnimationActorBinding[] ResolveActorBindings()
        {
            var resolved = new List<AnimationActorBinding>();
            if (actorBindings != null)
            {
                for (int i = 0; i < actorBindings.Length; i++)
                {
                    var existing = actorBindings[i];
                    if (existing != null && existing.IsValid)
                    {
                        resolved.Add(existing);
                    }
                }
            }

            if (autoPopulateActorBindings)
            {
                PopulateMissingActors(resolved);
                actorBindings = resolved.ToArray();
            }

            if (autoScanBindings)
            {
                AppendProviderBindings(resolved);
            }

            return resolved.ToArray();
        }

        private void PopulateMissingActors(List<AnimationActorBinding> bindings)
        {
            var knownActors = new HashSet<CombatantState>();
            for (int i = 0; i < bindings.Count; i++)
            {
                if (bindings[i].Actor != null)
                {
                    knownActors.Add(bindings[i].Actor);
                }
            }

#if UNITY_2022_1_OR_NEWER
            var actorsInScene = FindObjectsByType<CombatantState>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
            var actorsInScene = FindObjectsOfType<CombatantState>();
#endif

            for (int i = 0; i < actorsInScene.Length; i++)
            {
                var actor = actorsInScene[i];
                if (actor == null || knownActors.Contains(actor))
                {
                    continue;
                }

                var animator = actor.GetComponentInChildren<Animator>(true);
                if (animator == null)
                {
                    Debug.LogWarning($"[AnimationSystemInstaller] Auto-populate could not find Animator for '{actor.name}'. Skipping binding.", actor);
                    continue;
                }

            bindings.Add(new AnimationActorBinding(actor, animator, defaultFallbackClip));
            knownActors.Add(actor);
        }
        }

        private void AppendProviderBindings(List<AnimationActorBinding> bindings)
        {
            if (bindings == null)
            {
                return;
            }

            bindingProviderSummaries.Clear();
            bindingProviderIssues.Clear();

            var providerActors = new HashSet<CombatantState>();
#if UNITY_2022_1_OR_NEWER
            var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
            var behaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
#endif

            int providerCount = 0;
            int bindingCount = 0;

            for (int i = 0; i < behaviours.Length; i++)
            {
                var behaviour = behaviours[i];
                if (behaviour == null || !behaviour.gameObject.scene.IsValid())
                {
                    continue;
                }

                if (behaviour is not IAnimationBindingProvider provider)
                {
                    continue;
                }

                providerCount++;
                int providerBindings = 0;
                IEnumerable<AnimationActorBinding> entries = null;

                try
                {
                    entries = provider.GetBindings();
                }
                catch (Exception ex)
                {
                    bindingProviderIssues.Add($"{behaviour.name}: exception during GetBindings ({ex.GetType().Name})");
                    continue;
                }

                if (entries == null)
                {
                    bindingProviderIssues.Add($"{behaviour.name}: returned null bindings");
                    continue;
                }

                foreach (var entry in entries)
                {
                    if (entry == null || !entry.IsValid)
                    {
                        bindingProviderIssues.Add($"{behaviour.name}: reported invalid binding");
                        continue;
                    }

                    UpsertBinding(bindings, entry);
                    providerActors.Add(entry.Actor);
                    providerBindings++;
                    bindingCount++;
                }

                bindingProviderSummaries.Add($"{behaviour.name}: {providerBindings} binding(s)");
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            StrategyLoggerBridge.Info("AnimInstaller", $"Auto-scan providers={providerCount}, bindings={bindingCount}");
#else
            _ = providerActors;
#endif
        }

        private static void UpsertBinding(List<AnimationActorBinding> bindings, AnimationActorBinding entry)
        {
            for (int i = 0; i < bindings.Count; i++)
            {
                if (bindings[i]?.Actor == entry.Actor)
                {
                    bindings[i] = entry;
                    return;
                }
            }

            bindings.Add(entry);
        }

        public void RegisterActor(
            CombatantState actor,
            Animator animatorOverride = null,
            AnimationClip fallbackOverride = null,
            Transform[] socketsOverride = null)
        {
            if (actor == null)
            {
                return;
            }

            var animator = animatorOverride != null ? animatorOverride : actor.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                Debug.LogWarning($"[AnimationSystemInstaller] RegisterActor could not find Animator for '{actor.name}'.", actor);
                return;
            }

            var fallback = fallbackOverride != null ? fallbackOverride : defaultFallbackClip;
            var binding = new AnimationActorBinding(actor, animator, fallback, socketsOverride);

            Debug.Log($"[AnimationSystemInstaller] Registered actor '{actor.name}' with animator '{animator.name}'.", actor);
            wrapperResolver?.AddOrUpdateBinding(binding);
            UpdateBindingArray(binding);

            var orchestrationWrapper = actor.GetComponentInChildren<BattleV2.Orchestration.Runtime.AnimatorWrapper>(true);
            if (orchestrationWrapper != null)
            {
                if (animatorOverride != null)
                {
                    orchestrationWrapper.OverrideAnimator(animatorOverride);
                }

                orchestrationWrapper.AssignOwner(actor);
                AnimatorRegistry.Instance.Register(orchestrationWrapper);
            }
        }

        private void UpdateBindingArray(AnimationActorBinding binding)
        {
            if (actorBindings == null)
            {
                actorBindings = new[] { binding };
                return;
            }

            for (int i = 0; i < actorBindings.Length; i++)
            {
                if (actorBindings[i] != null && actorBindings[i].Actor == binding.Actor)
                {
                    actorBindings[i] = binding;
                    return;
                }
            }

            var list = new List<AnimationActorBinding>(actorBindings) { binding };
            actorBindings = list.ToArray();
        }

        private StepScheduler BuildStepScheduler(
            IMainThreadInvoker mainThreadInvoker,
            ITweenBindingResolver tweenResolver,
            ITweenBridge tweenBridge)
        {
            var scheduler = new StepScheduler();
            scheduler.ConfigureObserverInvoker(mainThreadInvoker);
            scheduler.ConfigureLifecycle(new ActionLifecycleConfig());
            scheduler.RegisterExecutor(new AnimatorClipExecutor());
            scheduler.RegisterExecutor(new FlipbookExecutor());
            scheduler.RegisterExecutor(new TweenExecutor());
            scheduler.RegisterExecutor(new WaitExecutor());
            scheduler.RegisterExecutor(new WaitSecondsExecutor());
            scheduler.RegisterExecutor(new TimedHitStepExecutor());
            scheduler.RegisterExecutor(new FlagExecutor());
            scheduler.RegisterExecutor(new SfxExecutor());
            scheduler.RegisterExecutor(new VfxExecutor());
            schedulerMetrics = new StepSchedulerMetricsObserver();
            scheduler.RegisterObserver(schedulerMetrics);
            return scheduler;
        }

        private ActionRecipeCatalog BuildRecipeCatalog(StepScheduler scheduler)
        {
            var builder = new ActionRecipeBuilder();
            var catalogInstance = new ActionRecipeCatalog();

            if (includePilotRecipes)
            {
                var recipes = new List<ActionRecipe>(PilotActionRecipes.Build(builder));
                catalogInstance.RegisterRange(recipes);

                for (int i = 0; i < recipes.Count; i++)
                {
                    scheduler.RegisterRecipe(recipes[i]);
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                ActionRecipeCatalogDiagnostics.ValidatePilotRecipes(catalogInstance);
#endif
            }

            return catalogInstance;
        }

        private IReadOnlyDictionary<BattlePhase, IPhaseStrategy> BuildPhaseStrategyMap()
        {
            return new Dictionary<BattlePhase, IPhaseStrategy>
            {
                { BattlePhase.None, NoOpPhaseStrategy.Instance },
                { BattlePhase.Intro, NoOpPhaseStrategy.Instance },
                { BattlePhase.Loop, NoOpPhaseStrategy.Instance },
                { BattlePhase.Turn, new TurnPhaseStrategy() },
                { BattlePhase.Outro, NoOpPhaseStrategy.Instance },
                { BattlePhase.Cinematic, NoOpPhaseStrategy.Instance }
            };
        }

        private IEnumerable<IRecipeExecutor> BuildRecipeExecutors(AnimationRouterBundle bundle)
        {
            if (bundle != null)
            {
                yield return new RouterRecipeExecutor(bundle, mainThreadInvoker ?? MainThreadInvoker.Instance);
            }

            if (recipeCatalog != null && stepScheduler != null)
            {
                yield return new StepSchedulerRecipeExecutor(
                    recipeCatalog,
                    stepScheduler,
                    AnimatorRegistry.Instance,
                    wrapperResolver,
                    routerBundle,
                    eventBus,
                    timedHitService,
                    mainThreadInvoker ?? MainThreadInvoker.Instance);
            }

            yield return NoOpRecipeExecutor.Instance;
        }

        private void RegisterInspectorRecipes()
        {
            if (recipeCatalog == null || stepScheduler == null || stepRecipeAssets == null)
            {
                return;
            }

            for (int i = 0; i < stepRecipeAssets.Length; i++)
            {
                var asset = stepRecipeAssets[i];
                if (asset == null)
                {
                    continue;
                }

                if (!asset.TryBuild(out var recipe) || recipe == null || recipe.IsEmpty)
                {
                    Debug.LogWarning($"[AnimationSystemInstaller] Recipe asset '{asset.name}' is empty or invalid. Skipping registration.", asset);
                    continue;
                }

                recipeCatalog.Register(recipe);
                stepScheduler.RegisterRecipe(recipe);
            }
        }

        private static T FindRunnerInstance<T>() where T : Component
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<T>();
#else
            return UnityEngine.Object.FindObjectOfType<T>();
#endif
        }
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (ks1TimedHitRunner == null)
            {
                ks1TimedHitRunner = FindRunnerInstance<Ks1TimedHitRunner>();
                if (ks1TimedHitRunner != null) UnityEditor.EditorUtility.SetDirty(this);
            }

            if (basicTimedHitRunner == null)
            {
                basicTimedHitRunner = FindRunnerInstance<BasicTimedHitRunner>();
                if (basicTimedHitRunner != null) UnityEditor.EditorUtility.SetDirty(this);
            }
        }

        [ContextMenu("[Debug] Dump Actor Bindings")]
        private void Debug_DumpActorBindings()
        {
            if (actorBindings == null || actorBindings.Length == 0)
            {
                Debug.Log("[AnimationSystemInstaller] actorBindings is null or empty.", this);
                return;
            }

            for (int i = 0; i < actorBindings.Length; i++)
            {
                var entry = actorBindings[i];
                if (entry == null)
                {
                    continue;
                }

                var actorName = entry.Actor != null ? entry.Actor.name : "(null)";
                var animatorTransform = entry.Animator != null ? entry.Animator.transform : null;
                var animatorId = animatorTransform != null ? animatorTransform.GetInstanceID() : -1;
                Debug.Log($"[ActorBinding] actor={actorName} animator={entry.Animator?.name ?? "(null)"} transformID={animatorId}", animatorTransform);

                if (entry.Sockets != null)
                {
                    for (int s = 0; s < entry.Sockets.Length; s++)
                    {
                        var socket = entry.Sockets[s];
                        if (socket == null)
                        {
                            continue;
                        }

                        Debug.Log($"[ActorBinding] actor={actorName} socket[{s}]={socket.name} id={socket.GetInstanceID()}", socket);
                    }
                }
            }
        }
#endif
    }
}
