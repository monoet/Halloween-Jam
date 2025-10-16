using UnityEngine;

[CreateAssetMenu(menuName = "JRPG/Derived Formulas/Moon Seer")]
public class MoonSeerFormula : DerivedFormula
{
    public override float AttackPower(FinalStats stats)
        => stats.AGI * 1.5f + stats.RES * 0.5f;

    public override float MagicPower(FinalStats stats)
        => stats.RES * 2.0f;

    public override float PhysDefense(FinalStats stats)
        => stats.VIT * 1.2f;

    public override float MagDefense(FinalStats stats)
        => stats.RES * 1.5f;

    public override float CritChance(FinalStats stats)
        => stats.LCK * 0.7f;

    public override float Speed(FinalStats stats)
        => stats.AGI * 1.4f;
}
