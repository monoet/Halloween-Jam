using System;
using BattleV2.AnimationSystem;
using BattleV2.AnimationSystem.Execution.Routers;
using BattleV2.Core;

namespace BattleV2.AnimationSystem.Runtime.Internal
{
    public sealed class AnimationRouterBundle : IDisposable
    {
        private readonly AnimationVfxRouter vfxRouter;
        private readonly AnimationSfxRouter sfxRouter;
        private readonly AnimationCameraRouter cameraRouter;
        private readonly AnimationUiRouter uiRouter;
        private readonly IAnimationVfxService vfxService;
        private readonly IAnimationSfxService sfxService;
        private readonly IAnimationCameraService cameraService;
        private readonly IAnimationUiService uiService;

        public AnimationRouterBundle(
            IAnimationEventBus eventBus,
            IAnimationVfxService vfxService,
            IAnimationSfxService sfxService,
            IAnimationCameraService cameraService,
            IAnimationUiService uiService)
        {
            this.vfxService = vfxService ?? new NullVfxService();
            this.sfxService = sfxService ?? new NullSfxService();
            this.cameraService = cameraService ?? new NullCameraService();
            this.uiService = uiService ?? new NullUiService();

            vfxRouter = new AnimationVfxRouter(eventBus, this.vfxService);
            sfxRouter = new AnimationSfxRouter(eventBus, this.sfxService);
            cameraRouter = new AnimationCameraRouter(eventBus, this.cameraService);
            uiRouter = new AnimationUiRouter(eventBus, this.uiService);
        }

        public void RegisterActor(CombatantState actor)
        {
            if (actor == null)
            {
                return;
            }

            vfxService.StopAllFor(actor);
            sfxService.StopAllFor(actor);
            cameraService.Reset(actor);
            uiService.Clear(actor);
        }

        public void UnregisterActor(CombatantState actor)
        {
            if (actor == null)
            {
                return;
            }

            vfxService.StopAllFor(actor);
            sfxService.StopAllFor(actor);
            cameraService.Reset(actor);
            uiService.Clear(actor);
        }

        public void Dispose()
        {
            vfxRouter?.Dispose();
            sfxRouter?.Dispose();
            cameraRouter?.Dispose();
            uiRouter?.Dispose();
        }

        private sealed class NullVfxService : IAnimationVfxService
        {
            public bool TryPlay(string vfxId, in AnimationImpactEvent evt, in AnimationEventPayload payload) => false;
            public void StopAllFor(CombatantState actor) { }
        }

        private sealed class NullSfxService : IAnimationSfxService
        {
            public bool TryPlay(string sfxId, CombatantState actor, AnimationImpactEvent? impactEvent, AnimationPhaseEvent? phaseEvent, in AnimationEventPayload payload) => false;
            public void StopAllFor(CombatantState actor) { }
        }

        private sealed class NullCameraService : IAnimationCameraService
        {
            public bool TryApply(string effectId, CombatantState actor, AnimationImpactEvent? impactEvent, AnimationPhaseEvent? phaseEvent, in AnimationEventPayload payload) => false;
            public void Reset(CombatantState actor) { }
        }

        private sealed class NullUiService : IAnimationUiService
        {
            public bool TryHandle(string uiId, CombatantState actor, AnimationPhaseEvent? phaseEvent, AnimationWindowEvent? windowEvent, AnimationImpactEvent? impactEvent, in AnimationEventPayload payload) => false;
            public void Clear(CombatantState actor) { }
        }
    }
}
