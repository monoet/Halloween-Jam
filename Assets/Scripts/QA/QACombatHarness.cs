using System.Threading.Tasks;
using BattleV2.AnimationSystem.Execution.Runtime.CombatEvents;
using UnityEngine;

namespace BattleV2.QA
{
    /// <summary>
    /// Simple harness that drives the CombatEventRouter for QA scenarios.
    /// </summary>
    public sealed class QACombatHarness : MonoBehaviour
    {
        [SerializeField] private CombatEventRouter router;
        [SerializeField] private QAActorRig actor;

        private void Awake()
        {
            router ??= FindObjectOfType<CombatEventRouter>();
            actor ??= FindObjectOfType<QAActorRig>();
        }

        public void FireFlag(string flagId)
        {
            if (router == null || actor == null || string.IsNullOrWhiteSpace(flagId))
            {
                return;
            }

            var context = actor.BuildContext();
            router.DispatchTestEvent(flagId, context);
            context.Release();
        }

        public void FireSequence(params string[] flags)
        {
            if (flags == null)
            {
                return;
            }

            for (int i = 0; i < flags.Length; i++)
            {
                FireFlag(flags[i]);
            }
        }

        public void FireCancelOffThread()
        {
            if (router == null || actor == null)
            {
                return;
            }

            _ = Task.Run(() =>
            {
                var context = actor.BuildContext();
                router.DispatchTestEvent(CombatEventFlags.ActionCancel, context);
                context.Release();
            });
        }

        public void DisableAnchor() => actor?.DisableAnchor();
        public void RemoveRoot() => actor?.RemoveRoot();
    }
}
