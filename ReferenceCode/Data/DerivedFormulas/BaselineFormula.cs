//📄 Assets/Scripts/Data/DerivedFormulas/BaselineFormula.cs
using UnityEngine;

[CreateAssetMenu(menuName = "JRPG/Derived Formulas/Baseline")]
public class BaselineFormula : DerivedFormula
{
    public override float AttackPower(CoreStats core) 
        => (core.BaseAGI + core.BonusAGI) * 1.2f; // ligera afinidad con AGI

    public override float MagicPower(CoreStats core)  
        => (core.BaseRES + core.BonusRES) * 1.2f; // ligera afinidad con RES

    public override float PhysDefense(CoreStats core) 
        => (core.BaseVIT + core.BonusVIT) * 1.0f;

    public override float MagDefense(CoreStats core)  
        => (core.BaseRES + core.BonusRES) * 1.0f;

    public override float CritChance(CoreStats core)  
        => (core.BaseLCK + core.BonusLCK) * 0.5f; // 0.5% por punto

    public override float Speed(CoreStats core)       
        => (core.BaseAGI + core.BonusAGI) * 1.5f; // AGI impacta turn order
}