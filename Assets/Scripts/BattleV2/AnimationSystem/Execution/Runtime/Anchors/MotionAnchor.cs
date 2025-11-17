using UnityEngine;

namespace BattleV2.AnimationSystem.Execution.Runtime.Anchors
{
    /// <summary>
    /// Marker component to assign as stable snapshot anchor for locomotion tweens.
    /// Does not modify transform; used only as a reference target.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MotionAnchor : MonoBehaviour
    {
    }
}
