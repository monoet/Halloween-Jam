// Assets/Scripts/Data/Abilities/AbilityData.cs
using UnityEngine;

[CreateAssetMenu(menuName = "RPG/Ability Data")]
public class AbilityData : ScriptableObject
{
    public string abilityName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Parámetros")]
    public int power;
    public int costSP;
    public float cooldown;
}
