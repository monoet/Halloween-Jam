// Assets/Scripts/Data/StatFormulas/LinearFormula.cs
using UnityEngine;

[CreateAssetMenu(menuName = "JRPG/Stat Formula/Linear")]
public class LinearFormula : ScriptableObject
{
    // === Fórmulas base ===
    public float STR(CoreStats s) => s.BaseSTR + s.BonusSTR;
    public float RES(CoreStats s) => s.BaseRES + s.BonusRES;
    public float AGI(CoreStats s) => s.BaseAGI + s.BonusAGI;
    public float LCK(CoreStats s) => s.BaseLCK + s.BonusLCK;
    public float VIT(CoreStats s) => s.BaseVIT + s.BonusVIT;
}
