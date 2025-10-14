/*📄 Assets/Scripts/Data/DerivedFormulas/AssassinFormula.cs
---------------------------------------------------------*/
using UnityEngine;

[CreateAssetMenu(menuName = "JRPG/Derived Formulas/Assassin")]
public class AssassinFormula : DerivedFormula
{
    public override float AttackPower(CoreStats core) 
        => (core.BaseAGI + core.BonusAGI) * 2.5f; // TODO: tuning

    public override float MagicPower(CoreStats core)  
        => 0f; // Assassin ignora magia

    public override float PhysDefense(CoreStats core) 
        => (core.BaseVIT + core.BonusVIT) * 0.8f; // más frágil

    public override float MagDefense(CoreStats core)  
        => (core.BaseRES + core.BonusRES) * 0.5f; // débil contra magia

    public override float CritChance(CoreStats core)  
        => (core.BaseLCK + core.BonusLCK) * 1.0f; // crítico altísimo

    public override float Speed(CoreStats core)       
        => (core.BaseAGI + core.BonusAGI) * 2.0f; // vuela en turn order
}
