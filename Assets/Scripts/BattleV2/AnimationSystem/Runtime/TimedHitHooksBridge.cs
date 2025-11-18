using BattleV2.Execution.TimedHits;
using BattleV2.Orchestration;
using BattleV2.Providers;
using UnityEngine;

namespace BattleV2.AnimationSystem.Runtime
{
    /// <summary>
    /// Exposes the currently active timed-hit runner so HUD/animation/audio bridges can hook without manual lookups.
    /// </summary>
    public sealed class TimedHitHooksBridge : MonoBehaviour
    {
        [SerializeField] private BattleManagerV2 manager;

        public ITimedHitRunner CurrentRunner => manager != null ? manager.TimedHitRunner : InstantTimedHitRunner.Shared;

        public ITimedHitRunner ResolveRunner(BattleSelection selection)
        {
            EnsureManager();
            return manager != null ? manager.ResolveTimedHitRunner(selection) : InstantTimedHitRunner.Shared;
        }

        private void Reset()
        {
            EnsureManager();
        }

        private void EnsureManager()
        {
            if (manager != null)
            {
                return;
            }

#if UNITY_2023_1_OR_NEWER
            manager = Object.FindFirstObjectByType<BattleManagerV2>();
#else
            manager = Object.FindObjectOfType<BattleManagerV2>();
#endif
        }
    }
}
