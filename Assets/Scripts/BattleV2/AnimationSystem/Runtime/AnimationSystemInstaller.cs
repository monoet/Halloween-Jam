using System;
using System.Collections.Generic;
using BattleV2.AnimationSystem.Catalog;
using BattleV2.AnimationSystem.Execution;
using BattleV2.AnimationSystem.Execution.Runtime;
using BattleV2.AnimationSystem.Execution.Runtime.Executors;
using BattleV2.AnimationSystem.Execution.Routers;
using BattleV2.AnimationSystem.Runtime.Internal;
using BattleV2.AnimationSystem.Timelines;
using BattleV2.Core;
using BattleV2.Orchestration.Runtime;
using UnityEngine;

namespace BattleV2.AnimationSystem.Runtime
{
    /// <summary>
    /// Scene-level composition root for the JRPG animation system.
    /// Instantiates core services and exposes an orchestrator implementation.
    /// </summary>
    public sealed class AnimationSystemInstaller : MonoBehaviour
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
        [Header("Timed Hit Settings")]
        [SerializeField] private TimedHitToleranceProfileAsset toleranceProfileAsset;

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

        public IAnimationEventBus EventBus => eventBus;
        public ICombatClock Clock => combatClock;
        public IActionLockManager LockManager => lockManager;
        public ITimedHitService TimedHitService => timedHitService;
        public IAnimationOrchestrator Orchestrator => orchestrator;
        public AnimationClipResolver ClipResolver => clipResolver;

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Debug.LogWarning("[AnimationSystemInstaller] Multiple installers detected. Replacing the active instance.");
            }

            Current = this;

            combatClock ??= new CombatClock();
            eventBus = new AnimationEventBus();
            lockManager = new ActionLockManager();
            timelineCompiler = new TimelineCompiler();
            runtimeBuilder = new TimelineRuntimeBuilder(timelineCompiler, combatClock, eventBus, lockManager);
            timedInputBuffer = new TimedInputBuffer(combatClock);
            toleranceProfile = DefaultTimedHitToleranceProfile.FromAsset(toleranceProfileAsset);
            timedHitService = new TimedHitService(combatClock, timedInputBuffer, toleranceProfile, eventBus);

            clipResolver = new AnimationClipResolver(clipBindings);
            wrapperResolver = new AnimatorWrapperResolver(ResolveActorBindings());

            var vfxService = ResolveService<IAnimationVfxService>(vfxServiceSource, "VFX");
            var sfxService = ResolveService<IAnimationSfxService>(sfxServiceSource, "SFX");
            var cameraService = ResolveService<IAnimationCameraService>(cameraServiceSource, "Camera");
            var uiService = ResolveService<IAnimationUiService>(uiServiceSource, "UI");

            routerBundle = new AnimationRouterBundle(eventBus, vfxService, sfxService, cameraService, uiService);
            stepScheduler = BuildStepScheduler();
            orchestrator = new NewAnimOrchestratorAdapter(
                runtimeBuilder,
                sequencerDriver,
                timelineCatalog,
                lockManager,
                eventBus,
                wrapperResolver,
                clipResolver,
                routerBundle,
                stepScheduler,
                AnimatorRegistry.Instance);

            if (sequencerDriver == null)
            {
                Debug.LogError("[AnimationSystemInstaller] Missing ActionSequencerDriver reference.", this);
            }

            if (timelineCatalog == null)
            {
                Debug.LogError("[AnimationSystemInstaller] Missing ActionTimelineCatalog reference.", this);
            }
        }

        private void OnDestroy()
        {
            timedHitService?.Dispose();
            routerBundle?.Dispose();
            wrapperResolver?.Dispose();
            AnimatorRegistry.Instance.Clear();
            if (Current == this)
            {
                Current = null;
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

        private StepScheduler BuildStepScheduler()
        {
            var scheduler = new StepScheduler();
            scheduler.RegisterExecutor(new AnimatorClipExecutor());
            scheduler.RegisterExecutor(new FlipbookExecutor());
            scheduler.RegisterExecutor(new TweenExecutor());
            scheduler.RegisterExecutor(new WaitExecutor());
            scheduler.RegisterExecutor(new SfxExecutor());
            scheduler.RegisterExecutor(new VfxExecutor());
            return scheduler;
        }
    }
}
