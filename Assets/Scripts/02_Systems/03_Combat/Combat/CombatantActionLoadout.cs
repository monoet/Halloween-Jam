using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central, asset-based source of truth for a combatant's allowed action ids.
/// When assigned to a CombatantState, this asset overrides the local per-prefab list.
/// </summary>
[CreateAssetMenu(menuName = "Battle/Combatant Action Loadout")]
public sealed class CombatantActionLoadout : ScriptableObject
{
    [SerializeField] private List<string> actionIds = new List<string>();
    public IReadOnlyList<string> ActionIds => actionIds;
}

