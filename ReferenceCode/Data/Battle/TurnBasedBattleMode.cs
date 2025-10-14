using System.Collections.Generic; // para Queue
using UnityEngine;

[CreateAssetMenu(menuName = "JRPG/BattleMode/TurnBased")]
public class TurnBasedBattleMode : BattleMode
{
    private Queue<CharacterRuntime> turnOrder = new();

    public override void Initialize(CharacterRuntime[] party, CharacterRuntime[] enemies)
    {
        var all = new List<CharacterRuntime>();
        all.AddRange(party);
        all.AddRange(enemies);

        // Ordenados por AGI
        all.Sort((a, b) => b.Final.AGI.CompareTo(a.Final.AGI));
        turnOrder = new Queue<CharacterRuntime>(all);
    }

    public override void UpdateBattle(float deltaTime)
    {
        // Turn-based no avanza en tiempo real
    }

    public override bool IsActionReady(CharacterRuntime crt)
    {
        if (turnOrder.Count == 0) return false;
        if (turnOrder.Peek() == crt)
        {
            turnOrder.Dequeue();
            return true;
        }
        return false;
    }
}
