using UnityEngine;

namespace BattleV2.AnimationSystem.Execution.Runtime.CombatEvents
{
    /// <summary>
    /// Quick sanity harness that fires the four combat flags on play.
    /// </summary>
    public sealed class CombatEventRouterQaHarness : MonoBehaviour
    {
        [SerializeField] private CombatEventRouter router;

        private void Start()
        {
            router ??= FindObjectOfType<CombatEventRouter>();
            if (router == null)
            {
                Debug.LogWarning("[CombatEventRouterQaHarness] CombatEventRouter not found in scene.");
                return;
            }

            var context = CombatEventContext.CreateStub();
            router.DispatchTestSequence(context);
        }
    }
}
