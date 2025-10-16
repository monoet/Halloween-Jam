using UnityEngine;

public abstract class DerivedFormula : ScriptableObject
{
    public abstract float AttackPower(FinalStats stats);
    public abstract float MagicPower(FinalStats stats);
    public abstract float PhysDefense(FinalStats stats);
    public abstract float MagDefense(FinalStats stats);
    public abstract float CritChance(FinalStats stats);
    public abstract float Speed(FinalStats stats);
}
