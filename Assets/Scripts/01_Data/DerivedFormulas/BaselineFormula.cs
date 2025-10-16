using UnityEngine;

[CreateAssetMenu(menuName = "JRPG/Derived Formulas/Baseline")]
public class BaselineFormula : DerivedFormula
{
    public override float AttackPower(FinalStats stats)
        => stats.AGI * 1.2f;

    public override float MagicPower(FinalStats stats)
        => stats.RES * 1.2f;

    public override float PhysDefense(FinalStats stats)
        => stats.VIT * 1.0f;

    public override float MagDefense(FinalStats stats)
        => stats.RES * 1.0f;

    public override float CritChance(FinalStats stats)
        => stats.LCK * 0.5f;

    public override float Speed(FinalStats stats)
        => stats.AGI * 1.5f;
}
