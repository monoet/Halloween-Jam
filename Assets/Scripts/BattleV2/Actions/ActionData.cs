using UnityEngine;
using BattleV2.Targeting;

namespace BattleV2.Actions
{
    [System.Serializable]
    public class BattleActionData
    {
        public string id;
        public string displayName;
        public int costSP;
        public int costCP;
        public ScriptableObject actionImpl;

        [Header("UI")]
        [TextArea]
        public string description;
        public Sprite elementIcon;

        [Header("Combat Event Options")]
        [Tooltip("Delay (seconds) between impact raises when the action hits multiple targets. 0 disables staggering.")]
        public float combatEventStaggerStep = 0f;

        [Header("Targeting")]
        public bool requiresTarget = true;
        public TargetAudience targetAudience = TargetAudience.Enemies;
        public TargetShape targetShape = TargetShape.Single;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (requiresTarget && targetAudience == TargetAudience.Self)
            {
                targetAudience = TargetAudience.Enemies;
            }
        }
#endif
    }
}
