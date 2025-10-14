using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Evalúa condiciones de victoria/derrota y emite eventos correspondientes.
/// </summary>
public class BattleEndManager : MonoBehaviour
{
    [System.Serializable]
    public class BattleFinishedEvent : UnityEvent<bool> { }

    [Header("Eventos")]
    [SerializeField] private BattleFinishedEvent onBattleFinished = new BattleFinishedEvent();

    public BattleFinishedEvent OnBattleFinished => onBattleFinished;

    public bool CheckBattleState(List<CharacterRuntime> partyMembers, List<CharacterRuntime> enemies)
    {
        bool partyAlive = HasLivingMembers(partyMembers);
        bool enemiesAlive = HasLivingMembers(enemies);

        if (!partyAlive || !enemiesAlive)
        {
            bool victory = partyAlive && !enemiesAlive;
            onBattleFinished.Invoke(victory);
            Debug.Log("[BattleEndManager] Combate concluido. Victoria: " + victory);
            return true;
        }

        return false;
    }

    private bool HasLivingMembers(List<CharacterRuntime> members)
    {
        if (members == null || members.Count == 0)
            return false;

        foreach (var member in members)
        {
            if (member == null)
                continue;
            var state = member.GetComponent<CombatantState>();
            if (state != null && state.IsAlive)
                return true;
        }

        return false;
    }
}
