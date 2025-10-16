using UnityEngine;

[CreateAssetMenu(menuName = "JRPG/Derived Formulas/Assassin")]
public class AssassinFormula : DerivedFormula
{
    public override float AttackPower(FinalStats stats)
        => stats.AGI * 2.5f;

    public override float MagicPower(FinalStats stats)
        => 0f;

    public override float PhysDefense(FinalStats stats)
        => stats.VIT * 0.8f;

    public override float MagDefense(FinalStats stats)
        => stats.RES * 0.5f;

    public override float CritChance(FinalStats stats)
        => stats.LCK * 1.0f;

    public override float Speed(FinalStats stats)
        => stats.AGI * 2.0f;
}
