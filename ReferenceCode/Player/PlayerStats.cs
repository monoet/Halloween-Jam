using UnityEngine;

[System.Serializable]
public class PlayerStats : MonoBehaviour
{
    public string characterName = "Lilia";
    public Sprite portrait; // retrato del personaje
    public int level = 1;

    public int currentHP = 50;
    public int maxHP = 100;
    public int currentSP = 20;
    public int maxSP = 40;
    public int currentEXP = 10;
    public int maxEXP = 100;
}

