using UnityEngine;

namespace BattleV2.Orchestration
{
    /// <summary>
    /// Describes an individual combatant spawn entry.
    /// </summary>
    [System.Serializable]
    public struct CombatantLoadoutEntry
    {
        [SerializeField] private GameObject prefab;
        [SerializeField] private Vector3 spawnOffset;
        [SerializeField] private ScriptableObject dropTable;

        public CombatantLoadoutEntry(GameObject prefab, Vector3 spawnOffset, ScriptableObject dropTable = null)
        {
            this.prefab = prefab;
            this.spawnOffset = spawnOffset;
            this.dropTable = dropTable;
        }

        public GameObject Prefab => prefab;
        public Vector3 SpawnOffset => spawnOffset;
        public ScriptableObject DropTable => dropTable;
        public bool IsValid => prefab != null;
    }
}
