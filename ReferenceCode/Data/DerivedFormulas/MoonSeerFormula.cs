/*📄 Assets/Scripts/Data/DerivedFormulas/MoonSeerFormula.cs
---------------------------------------------------------*/
using UnityEngine;

[CreateAssetMenu(menuName = "JRPG/Derived Formulas/Moon Seer")]
public class MoonSeerFormula : DerivedFormula
{
    public override float AttackPower(CoreStats core) 
        => (core.BaseAGI + core.BonusAGI) * 1.5f 
         + (core.BaseRES + core.BonusRES) * 0.5f; // híbrido físico + místico

    public override float MagicPower(CoreStats core)  
        => (core.BaseRES + core.BonusRES) * 2.0f; // magia potenciada

    public override float PhysDefense(CoreStats core) 
        => (core.BaseVIT + core.BonusVIT) * 1.2f;

    public override float MagDefense(CoreStats core)  
        => (core.BaseRES + core.BonusRES) * 1.5f;

    public override float CritChance(CoreStats core)  
        => (core.BaseLCK + core.BonusLCK) * 0.7f;

    public override float Speed(CoreStats core)       
        => (core.BaseAGI + core.BonusAGI) * 1.4f;
}