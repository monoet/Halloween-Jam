using UnityEngine;

namespace BattleV2.Orchestration
{
    /// <summary>
    /// Defines the prefab and runtime data required to spawn an enemy at battle start.
    /// </summary>
    [CreateAssetMenu(menuName = "Loadouts/Enemy Loadout")]
    public class EnemyLoadout : ScriptableObject
    {
        [Header("Prefab")]
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField] private Vector3 spawnOffset;

        [Header("Drops (optional)")]
        [SerializeField] private ScriptableObject dropTable;

        public GameObject EnemyPrefab => enemyPrefab;
        public Vector3 SpawnOffset => spawnOffset;
        public ScriptableObject DropTable => dropTable;

        public bool IsValid => enemyPrefab != null;
    }
}
