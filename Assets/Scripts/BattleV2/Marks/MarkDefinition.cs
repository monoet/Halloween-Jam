using UnityEngine;

namespace BattleV2.Marks
{
    /// <summary>
    /// Data describing a mark effect. Gameplay resolution is handled elsewhere.
    /// </summary>
    [CreateAssetMenu(fileName = "MarkDefinition", menuName = "BattleV2/Marks/Definition")]
    public sealed class MarkDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string id;
        [Tooltip("Element identifier. If empty, falls back to id.")]
        public string elementId;

        [Header("Logic Flags")]
        public bool canBeAppliedAsMark = true;
        public bool canDetonateMarks = true;
        [Min(1)]
        public int baseDurationTurns = 1;

        [Header("UI")]
        public Sprite icon;
        public Color tint = Color.white;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if ((canBeAppliedAsMark || canDetonateMarks) && string.IsNullOrWhiteSpace(elementId))
            {
                Debug.LogError($"[MarkDefinition] elementId is required for mark '{name}' when it can be applied/detonated. Please set elementId.", this);
            }
        }
#endif
    }
}
