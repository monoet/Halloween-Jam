using UnityEngine;

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

        [Header("Combat Event Options")]
        [Tooltip("Delay (seconds) between impact raises when the action hits multiple targets. 0 disables staggering.")]
        public float combatEventStaggerStep = 0f;
    }
}
