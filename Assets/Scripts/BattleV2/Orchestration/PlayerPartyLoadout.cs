using System.Collections.Generic;
using UnityEngine;

namespace BattleV2.Orchestration
{
    [CreateAssetMenu(menuName = "BattleV2/Loadouts/Player Party")]
    public class PlayerPartyLoadout : ScriptableObject
    {
        [SerializeField] private List<CombatantLoadoutEntry> members = new();

        public IReadOnlyList<CombatantLoadoutEntry> Members => members;
    }
}
