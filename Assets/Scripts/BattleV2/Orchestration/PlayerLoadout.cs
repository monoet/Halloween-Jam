using UnityEngine;

namespace BattleV2.Orchestration
{
    /// <summary>
    /// Defines the prefab and optional runtime overrides required to spawn the player at battle start.
    /// </summary>
    [CreateAssetMenu(menuName = "Loadouts/Player Loadout")]
    public class PlayerLoadout : ScriptableObject
    {
        [Header("Prefab")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private Vector3 spawnOffset;

        public GameObject PlayerPrefab => playerPrefab;
        public Vector3 SpawnOffset => spawnOffset;

        public bool IsValid => playerPrefab != null;
    }
}
