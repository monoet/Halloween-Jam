using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Calcula y gestiona el orden de turnos de los combatientes.
/// </summary>
public class TurnController : MonoBehaviour
{
    [SerializeField] private Queue<CharacterRuntime> turnQueue = new Queue<CharacterRuntime>();

    public void CalculateTurnOrder(List<CharacterRuntime> combatants)
    {
        turnQueue.Clear();

        if (combatants == null || combatants.Count == 0)
        {
            Debug.LogWarning("[TurnController] Lista de combatientes vacía.");
            return;
        }

        // TODO: Integrar Speed y modificadores de estado para ordenar.
        foreach (var combatant in combatants)
        {
            if (combatant == null)
                continue;

            turnQueue.Enqueue(combatant);
            Debug.Log("[TurnController] Añadido a la cola: " + combatant.Archetype.characterName);
        }
    }

    public CharacterRuntime NextTurn()
    {
        if (turnQueue.Count == 0)
        {
            Debug.LogWarning("[TurnController] Cola de turnos vacía.");
            return null;
        }

        var next = turnQueue.Dequeue();
        turnQueue.Enqueue(next); // rota la cola para un loop simple
        return next;
    }

    public void RebuildQueue()
    {
        var temp = new List<CharacterRuntime>(turnQueue);
        CalculateTurnOrder(temp);
    }

    public void Clear()
    {
        turnQueue.Clear();
    }
}
