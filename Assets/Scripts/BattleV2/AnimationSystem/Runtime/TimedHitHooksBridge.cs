using BattleV2.AnimationSystem.Execution.Runtime;
using BattleV2.Execution.TimedHits;
using BattleV2.Providers;
using UnityEngine;

namespace BattleV2.AnimationSystem.Runtime
{
    /// <summary>
    /// Exposes the currently active timed-hit runner so HUD/animation/audio bridges can hook without manual lookups.
    /// </summary>
    public sealed class TimedHitHooksBridge : MonoBehaviour
    {
        [SerializeField] private AnimationSystemInstaller installer;

        public ITimedHitRunner CurrentRunner
        {
            get
            {
                var service = ResolveService();
                return service?.GetRunner(TimedHitRunnerKind.Default) ?? InstantTimedHitRunner.Shared;
            }
        }

        public ITimedHitRunner ResolveRunner(BattleSelection selection)
        {
            var service = ResolveService();
            if (service == null)
            {
                return InstantTimedHitRunner.Shared;
            }

            var kind = selection.RunnerKind == TimedHitRunnerKind.Basic
                ? TimedHitRunnerKind.Basic
                : TimedHitRunnerKind.Default;

            return service.GetRunner(kind) ?? InstantTimedHitRunner.Shared;
        }

        private void Reset()
        {
            installer ??= AnimationSystemInstaller.Current;
        }

        private ITimedHitService ResolveService()
        {
            installer ??= AnimationSystemInstaller.Current;
            return installer != null ? installer.TimedHitService : null;
        }
    }
}
