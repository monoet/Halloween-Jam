using System.Collections.Generic;
using UnityEngine;

namespace BattleV2.Orchestration
{
    [CreateAssetMenu(menuName = "BattleV2/Loadouts/Enemy Encounter")]
    public class EnemyEncounterLoadout : ScriptableObject
    {
        [SerializeField] private List<CombatantLoadoutEntry> enemies = new();
        [SerializeField] private EncounterSpawnPattern spawnPattern;
        [SerializeField] private bool useSceneAnchors = true;
        [SerializeField] private Vector3 fallbackOriginOffset = Vector3.zero;

        public IReadOnlyList<CombatantLoadoutEntry> Enemies => enemies;
        public EncounterSpawnPattern SpawnPattern => spawnPattern;
        public bool UseSceneAnchors => useSceneAnchors;
        public Vector3 FallbackOriginOffset => fallbackOriginOffset;
    }
}
