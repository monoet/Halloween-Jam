using UnityEngine;

[System.Serializable]
public class Perk
{
    public string perkName;
    public string description;

    [Header("Bonos otorgados")]
    public float bonusSTR;
    public float bonusRES;
    public float bonusAGI;
    public float bonusLCK;
    public float bonusVIT;

    public void Apply(CharacterRuntime runtime)
    {
        runtime?.AddBonus(bonusSTR, bonusRES, bonusAGI, bonusLCK, bonusVIT);
    }

    public void Remove(CharacterRuntime runtime)
    {
        runtime?.RemoveBonus(bonusSTR, bonusRES, bonusAGI, bonusLCK, bonusVIT);
    }
}
