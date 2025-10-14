/*📄 Assets/Scripts/Data/DerivedFormulas/DerivedFormula.cs
--------------------------------------------------------*/
using UnityEngine;

public abstract class DerivedFormula : ScriptableObject
{
    public abstract float AttackPower(CoreStats core);
    public abstract float MagicPower(CoreStats core);
    public abstract float PhysDefense(CoreStats core);
    public abstract float MagDefense(CoreStats core);
    public abstract float CritChance(CoreStats core);
    public abstract float Speed(CoreStats core);
}
