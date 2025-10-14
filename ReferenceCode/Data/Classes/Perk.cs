using UnityEngine;

[System.Serializable]
public class Perk
{
    public string perkName;
    public string description;

    [Header("Bonus que otorga este perk")]
    public float bonusSTR;
    public float bonusRES;
    public float bonusAGI;
    public float bonusLCK;
    public float bonusVIT;

    // Aplica el perk a un CharacterRuntime
    public void Apply(CharacterRuntime crt)
    {
        if (crt == null) return;
        crt.AddBonus(bonusSTR, bonusRES, bonusAGI, bonusLCK, bonusVIT);
    }

    // Remueve el perk de un CharacterRuntime
    public void Remove(CharacterRuntime crt)
    {
        if (crt == null) return;
        crt.RemoveBonus(bonusSTR, bonusRES, bonusAGI, bonusLCK, bonusVIT);
    }
}
