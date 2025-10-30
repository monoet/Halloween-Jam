using System;
using BattleV2.AnimationSystem.Catalog;
using BattleV2.AnimationSystem.Execution;
using BattleV2.AnimationSystem.Execution.Runtime;
using BattleV2.AnimationSystem.Execution.Routers;
using BattleV2.AnimationSystem.Timelines;
using BattleV2.Core;
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
        [SerializeField] private AnimationActorBinding[] actorBindings;
        [SerializeField] private AnimationClipBinding[] clipBindings;
        [SerializeField] private UnityEngine.Object vfxServiceSource;
        [SerializeField] private UnityEngine.Object sfxServiceSource;
        [SerializeField] private UnityEngine.Object cameraServiceSource;
        [SerializeField] private UnityEngine.Object uiServiceSource;

        private AnimationEventBus eventBus;
        private ActionLockManager lockManager;
        private TimelineCompiler timelineCompiler;
        private TimelineRuntimeBuilder runtimeBuilder;
        private TimedInputBuffer timedInputBuffer;
        private DefaultTimedHitToleranceProfile toleranceProfile;
        private TimedHitService timedHitService;
        private AnimatorWrapperResolver wrapperResolver;
        private AnimationRouterBundle routerBundle;
        private NewAnimOrchestratorAdapter orchestrator;

        public IAnimationEventBus EventBus => eventBus;
        public ICombatClock Clock => combatClock;
        public IActionLockManager LockManager => lockManager;
        public ITimedHitService TimedHitService => timedHitService;
        public IAnimationOrchestrator Orchestrator => orchestrator;

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
            toleranceProfile = new DefaultTimedHitToleranceProfile();
            timedHitService = new TimedHitService(combatClock, timedInputBuffer, toleranceProfile, eventBus);

            var clipResolver = new AnimationClipResolver(clipBindings);
            wrapperResolver = new AnimatorWrapperResolver(actorBindings);

            var vfxService = ResolveService<IAnimationVfxService>(vfxServiceSource, "VFX");
            var sfxService = ResolveService<IAnimationSfxService>(sfxServiceSource, "SFX");
            var cameraService = ResolveService<IAnimationCameraService>(cameraServiceSource, "Camera");
            var uiService = ResolveService<IAnimationUiService>(uiServiceSource, "UI");

            routerBundle = new AnimationRouterBundle(eventBus, vfxService, sfxService, cameraService, uiService);
            orchestrator = new NewAnimOrchestratorAdapter(
                runtimeBuilder,
                sequencerDriver,
                timelineCatalog,
                lockManager,
                eventBus,
                wrapperResolver,
                clipResolver,
                routerBundle);

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

    }
}
