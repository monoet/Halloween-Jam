using BattleV2.AnimationSystem.Execution.Runtime.CombatEvents;
using UnityEngine;

namespace BattleV2.QA
{
    /// <summary>
    /// Inspector buttons for driving QACombatHarness without custom editor tooling.
    /// </summary>
    public sealed class QAButtons : MonoBehaviour
    {
            [SerializeField] private QACombatHarness harness;

            private void Awake()
            {
                harness ??= GetComponent<QACombatHarness>();
            }

            [ContextMenu("Flag/Windup")]
            public void Windup() => harness?.FireFlag(CombatEventFlags.Windup);

            [ContextMenu("Flag/Runup")]
            public void Runup() => harness?.FireFlag(CombatEventFlags.Runup);

            [ContextMenu("Flag/Impact")]
            public void Impact() => harness?.FireFlag(CombatEventFlags.Impact);

            [ContextMenu("Flag/Runback")]
            public void Runback() => harness?.FireFlag(CombatEventFlags.Runback);

            [ContextMenu("Flag/Cancel")]
            public void Cancel() => harness?.FireFlag(CombatEventFlags.ActionCancel);

            [ContextMenu("Anchors/Disable Anchor")]
            public void DisableAnchor() => harness?.DisableAnchor();

            [ContextMenu("Anchors/Remove Root")]
            public void RemoveRoot() => harness?.RemoveRoot();

            [ContextMenu("Cancel/Early (Off Thread)")]
            public void EarlyCancelOffThread() => harness?.FireCancelOffThread();
    }
}
