using System.Collections.Generic;
using UnityEngine;

namespace BattleV2.Orchestration
{
    [CreateAssetMenu(menuName = "BattleV2/Loadouts/Enemy Encounter")]
    public class EnemyEncounterLoadout : ScriptableObject
    {
        [SerializeField] private List<CombatantLoadoutEntry> enemies = new();

        public IReadOnlyList<CombatantLoadoutEntry> Enemies => enemies;
    }
}
