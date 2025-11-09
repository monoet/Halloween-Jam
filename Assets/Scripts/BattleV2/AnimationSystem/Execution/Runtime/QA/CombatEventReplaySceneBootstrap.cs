using UnityEngine;

namespace BattleV2.AnimationSystem.Execution.Runtime.CombatEvents
{
    /// <summary>
    /// Minimal bootstrapper for the CombatEvents replay QA scene.
    /// </summary>
    public sealed class CombatEventReplaySceneBootstrap : MonoBehaviour
    {
        [SerializeField] private bool autoBootstrap = true;

        private void Awake()
        {
            if (!autoBootstrap)
            {
                return;
            }

            BuildRouterRig();
        }

        private void BuildRouterRig()
        {
            var routerGo = new GameObject("CombatEventRouter_QA");
            var router = routerGo.AddComponent<CombatEventRouter>();
            routerGo.AddComponent<CombatEventRouterPresetSeeder>();
            var tweenListener = routerGo.AddComponent<DOTweenListener>();
            var sfxListener = routerGo.AddComponent<FMODListener>();
            router.ConfigureListeners(tweenListener, sfxListener);

            var runnerGo = new GameObject("EventReplayRunner_QA");
            var runner = runnerGo.AddComponent<EventReplayRunner>();
            runner.Router = router;
            runner.Play();
        }
    }
}
