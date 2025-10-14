// Assets/Scripts/Runtime/Strategies/Equipment/EquipmentPolicy.cs
using UnityEngine;

public abstract class EquipmentPolicy : ScriptableObject
{
    public abstract bool CanEquip(CharacterRuntime crt, EquipmentItem item);
    public abstract void OnEquipped(CharacterRuntime crt, EquipmentItem item);
    public abstract void OnUnequipped(CharacterRuntime crt, EquipmentItem item);
}
