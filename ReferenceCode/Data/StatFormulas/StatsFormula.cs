using UnityEngine;

public abstract class StatFormula : ScriptableObject
{
    public abstract int MaxHP(CoreStats s);
    public abstract int MaxMP(CoreStats s);
    public abstract float STR(CoreStats s);
    public abstract float RES(CoreStats s);
    public abstract float AGI(CoreStats s);
    public abstract float LCK(CoreStats s);
    public abstract float VIT(CoreStats s);
}
