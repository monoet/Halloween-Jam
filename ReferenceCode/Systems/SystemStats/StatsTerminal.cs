/* using UnityEngine;

public class StatsTerminal : MonoBehaviour
{
    [SerializeField] private CharacterRuntime crt;

    private void Start()
    {
        if (crt == null)
        {
            Debug.LogWarning("StatsTerminal: No CharacterRuntime assigned!");
            return;
        }

        PrintStats();
    }

    public void PrintStats()
    {
        CoreStats c = crt.Core;
        FinalStats f = crt.Final;

        // Core
        Debug.Log($"Level: {c.Level}");
        Debug.Log($"BaseHP: {c.BaseHP} | HP (final): {f.HP}");
        Debug.Log($"BaseSTR: {c.BaseSTR} | STR (final): {f.STR}");
        Debug.Log($"BaseRES: {c.BaseRES} | RES (final): {f.RES}");
        Debug.Log($"BaseAGI: {c.BaseAGI} | AGI (final): {f.AGI}");
        Debug.Log($"BaseLCK: {c.BaseLCK} | LCK (final): {f.LCK}");
        Debug.Log($"BaseVIT: {c.BaseVIT} | VIT (final): {f.VIT}");

        // Derived Combat
        Debug.Log("=== Combat Stats ===");
        Debug.Log($"Physical: {f.Physical}");
        Debug.Log($"MagicPower: {f.MagicPower}");
        Debug.Log($"PhysDefense: {f.PhysDefense}");
        Debug.Log($"MagDefense: {f.MagDefense}");
        Debug.Log($"CritChance: {f.CritChance}");
        Debug.Log($"Speed: {f.Speed}");
    }
}

*/