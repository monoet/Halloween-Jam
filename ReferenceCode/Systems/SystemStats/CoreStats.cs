// Assets/Scripts/Systems/SystemStats/CoreStats.cs
using UnityEngine;

[System.Serializable]
public struct CoreStats
{
    [Header("Identidad del Personaje (solo lectura visual)")]
    public string characterName;
    public Sprite portrait;

    [Header("Nivel y experiencia")]
    public int Level;
    public int CurrentEXP;
    public int MaxEXP;

    [Header("Stats base (nivel 1)")]
    public float BaseSTR;
    public float BaseRES;
    public float BaseAGI;
    public float BaseLCK;
    public float BaseVIT;

    [Header("Bonificaciones (por equipo, perks o buffs)")]
    public float BonusSTR;
    public float BonusRES;
    public float BonusAGI;
    public float BonusLCK;
    public float BonusVIT;

    [Header("Estados temporales")]
    public bool IsPoisoned;
    public bool IsStunned;
    public bool IsBuffed;

    // ===============================
    // === Metodos de conveniencia ===
    // ===============================

    public float TotalSTR => BaseSTR + BonusSTR;
    public float TotalRES => BaseRES + BonusRES;
    public float TotalAGI => BaseAGI + BonusAGI;
    public float TotalLCK => BaseLCK + BonusLCK;
    public float TotalVIT => BaseVIT + BonusVIT;

    public void ResetBonuses()
    {
        BonusSTR = 0f;
        BonusRES = 0f;
        BonusAGI = 0f;
        BonusLCK = 0f;
        BonusVIT = 0f;
    }
}
