using UnityEngine;

public class RuntimeStatsDebug : MonoBehaviour
{
    [SerializeField] private CharacterRuntime liliaRuntime;
    [SerializeField] private bool autoRecalc = true; // ✅ recalcula cada frame para debug en vivo

    private void OnGUI()
    {
        if (liliaRuntime == null) return;

        // 🔁 recalcula si está activado
        if (autoRecalc) liliaRuntime.Recalc();

        var final = liliaRuntime.Final;
        var core  = liliaRuntime.Core;
        var arche = liliaRuntime.Archetype;
        var derived = liliaRuntime.GetComponent<CharacterRuntime>().GetComponent<CharacterRuntime>();

        GUIStyle header = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            normal = { textColor = Color.green }
        };

        GUIStyle sub = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            normal = { textColor = Color.cyan }
        };

        GUILayout.BeginArea(new Rect(10, 10, 420, 600), GUI.skin.box);
        GUILayout.Label($"<b>{arche.characterName}</b>  (Lv {core.Level})", header);

        GUILayout.Space(6);
        GUILayout.Label($"HP: {final.HP}", style);
        GUILayout.Label($"SP: {final.SP}", style);

        GUILayout.Space(10);
        GUILayout.Label("=== BASE + BONUS ===", sub);
        GUILayout.Label($"STR: {core.BaseSTR:F1} + {core.BonusSTR:F1}", style);
        GUILayout.Label($"RES: {core.BaseRES:F1} + {core.BonusRES:F1}", style);
        GUILayout.Label($"AGI: {core.BaseAGI:F1} + {core.BonusAGI:F1}", style);
        GUILayout.Label($"VIT: {core.BaseVIT:F1} + {core.BonusVIT:F1}", style);
        GUILayout.Label($"LCK: {core.BaseLCK:F1} + {core.BonusLCK:F1}", style);

        GUILayout.Space(10);
        GUILayout.Label("=== FINAL ===", sub);
        GUILayout.Label($"STR: {final.STR:F1}", style);
        GUILayout.Label($"RES: {final.RES:F1}", style);
        GUILayout.Label($"AGI: {final.AGI:F1}", style);
        GUILayout.Label($"VIT: {final.VIT:F1}", style);
        GUILayout.Label($"LCK: {final.LCK:F1}", style);

        GUILayout.Space(10);
        GUILayout.Label("=== DERIVED ===", sub);
        GUILayout.Label($"PhysAtk: {final.Physical:F1}", style);
        GUILayout.Label($"MagPower: {final.MagicPower:F1}", style);
        GUILayout.Label($"PhysDef: {final.PhysDefense:F1}", style);
        GUILayout.Label($"MagDef: {final.MagDefense:F1}", style);
        GUILayout.Label($"Crit: {final.CritChance:F1}", style);
        GUILayout.Label($"Speed: {final.Speed:F1}", style);

        GUILayout.EndArea();
    }
}
