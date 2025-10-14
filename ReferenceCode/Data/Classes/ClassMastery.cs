using UnityEngine;

[CreateAssetMenu(fileName = "ClassMastery", menuName = "RPG/Class Mastery")]
public class ClassMastery : ScriptableObject
{
    [Header("Identidad de la clase")]
    public string className; // e.g. Warrior, Mage, Ranger
    [TextArea(2, 4)] public string description;

    [Header("Bonificadores por rango (1–5)")]
    [Tooltip("Cada array debe tener la misma longitud. Define los bonos acumulativos por rango.")]
    public int[] strBonusPerRank = new int[5];
    public int[] resBonusPerRank = new int[5];
    public int[] agiBonusPerRank = new int[5];
    public int[] lckBonusPerRank = new int[5];
    public int[] vitBonusPerRank = new int[5];

    [Header("Perks desbloqueables por rango")]
    [Tooltip("Cada rango puede otorgar perks o pasivas únicas.")]
    public Perk[] perks = new Perk[5];

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Sincroniza longitudes de todos los arrays
        int targetLength = Mathf.Max(
            strBonusPerRank?.Length ?? 0,
            resBonusPerRank?.Length ?? 0,
            agiBonusPerRank?.Length ?? 0,
            lckBonusPerRank?.Length ?? 0,
            vitBonusPerRank?.Length ?? 0,
            perks?.Length ?? 0,
            5 // longitud mínima por defecto
        );

        EnsureLength(ref strBonusPerRank, targetLength);
        EnsureLength(ref resBonusPerRank, targetLength);
        EnsureLength(ref agiBonusPerRank, targetLength);
        EnsureLength(ref lckBonusPerRank, targetLength);
        EnsureLength(ref vitBonusPerRank, targetLength);
        EnsureLength(ref perks, targetLength);
    }

    private void EnsureLength<T>(ref T[] array, int target)
    {
        if (array == null || array.Length != target)
        {
            T[] newArray = new T[target];
            if (array != null)
                System.Array.Copy(array, newArray, Mathf.Min(array.Length, target));
            array = newArray;
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}
