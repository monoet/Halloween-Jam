using UnityEngine;

[CreateAssetMenu(menuName = "RPG/Equipment Data")]
public class EquipmentData : ScriptableObject
{
    [Header("Identidad del equipo")]
    public string equipmentName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Bonificaciones de stats")]
    public int bonusSTR;
    public int bonusRES;
    public int bonusAGI;
    public int bonusLCK;
    public int bonusVIT;

    [Header("Tipo de equipo")]
    public EquipmentType type = EquipmentType.Weapon;
}

public enum EquipmentType
{
    Weapon,
    Armor,
    Accessory,
    Other
}
