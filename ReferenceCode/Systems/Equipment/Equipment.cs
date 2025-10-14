using UnityEngine;


public enum EquipmentSlot
{
    Head,
    Chest,
    Legs,
    Weapon,
    Accessory
}

public enum EquipmentWeight
{
    Light,
    Medium,
    Heavy
}


[CreateAssetMenu(fileName = "Equipment", menuName = "RPG/Equipment")]
public class Equipment : ScriptableObject
{
    public string itemName;
    public EquipmentSlot slot;   // Aquí defines a qué parte pertenece
    public EquipmentWeight weight;

    // Stats que modifica
    public int strMod;
    public int resMod;
    public int agiMod;
    public int lckMod;
    public int vitMod;
}
