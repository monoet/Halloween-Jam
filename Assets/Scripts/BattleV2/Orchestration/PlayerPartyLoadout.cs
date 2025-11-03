using System.Collections.Generic;
using UnityEngine;

namespace BattleV2.Orchestration
{
    [CreateAssetMenu(menuName = "Loadouts/Player Party")]
    public class PlayerPartyLoadout : ScriptableObject
    {
        [SerializeField] private List<CombatantLoadoutEntry> members = new();
        [SerializeField] private EncounterSpawnPattern spawnPattern;

        public IReadOnlyList<CombatantLoadoutEntry> Members => members;
        public EncounterSpawnPattern SpawnPattern => spawnPattern;
    }
}
