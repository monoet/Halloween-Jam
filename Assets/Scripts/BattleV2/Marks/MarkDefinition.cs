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

        [Header("UI")]
        public Sprite icon;
        public Color tint = Color.white;
    }
}
