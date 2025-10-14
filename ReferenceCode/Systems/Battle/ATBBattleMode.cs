using System.Collections.Generic; // para Dictionary
using UnityEngine;

[CreateAssetMenu(menuName = "JRPG/BattleMode/ATB")]
public class ATBBattleMode : BattleMode
{
    private Dictionary<CharacterRuntime, float> gauges = new();

    public override void Initialize(CharacterRuntime[] party, CharacterRuntime[] enemies)
    {
        gauges.Clear();
        foreach (var crt in party) gauges[crt] = 0;
        foreach (var crt in enemies) gauges[crt] = 0;
    }

    public override void UpdateBattle(float deltaTime)
    {
        List<CharacterRuntime> keys = new List<CharacterRuntime>(gauges.Keys);
        foreach (var crt in keys)
        {
            gauges[crt] += crt.Final.AGI * deltaTime;
        }
    }

    public override bool IsActionReady(CharacterRuntime crt)
    {
        if (!gauges.ContainsKey(crt)) return false;
        if (gauges[crt] >= 100f)
        {
            gauges[crt] = 0f;
            return true;
        }
        return false;
    }
}
