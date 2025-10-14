using UnityEngine;
using TMPro;

public class StatsHUD : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CharacterRuntime crt;
    [SerializeField] private TextMeshProUGUI uiText;

    private void Update()
    {
        if (crt == null || uiText == null) return;

        var f = crt.Final;
        var c = crt.Core;

        uiText.text =
            $"<b>Level {crt.Level}</b>\n" +
            $"HP: {f.HP}   MP: {f.SP}\n" +
            $"STR: {f.STR}   RES: {f.RES}\n" +
            $"AGI: {f.AGI}   LCK: {f.LCK}   VIT: {f.VIT}\n\n" +
            $"<b>=== Combat Stats ===</b>\n" +
            $"Physical: {f.Physical}\n" +
            $"MagicPower: {f.MagicPower}\n" +
            $"PhysDefense: {f.PhysDefense}\n" +
            $"MagDefense: {f.MagDefense}\n" +
            $"CritChance: {f.CritChance}\n" +
            $"Speed: {f.Speed}";
    }
}
