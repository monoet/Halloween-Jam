using UnityEngine;

namespace BattleV2.Actions
{
    [System.Serializable]
    public class ActionData
    {
        public string id;
        public string displayName;
        public int costSP;
        public int costCP;
        public ScriptableObject actionImpl;
    }
}
