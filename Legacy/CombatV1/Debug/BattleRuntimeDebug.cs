using UnityEngine;
using HalloweenJam.Combat;

/// <summary>
/// Runtime debug HUD para batalla: botones de ataque / enemigo y overlay con flags.
/// </summary>
public class BattleRuntimeDebug : MonoBehaviour
{
    private BattleManager battleManager;
    private BattleOrchestrator orchestrator;

    [Header("UI Config")]
    [SerializeField] private Vector2 buttonSize = new Vector2(140f, 40f);
    [SerializeField] private float margin = 10f;
    [SerializeField] private bool showStatusOverlay = true;

    private GUIStyle buttonStyle;
    private GUIStyle labelStyle;
    private GUIStyle headerStyle;

    private void Start()
    {
        battleManager = FindObjectOfType<BattleManager>();
        if (battleManager == null)
        {
            Debug.LogWarning("[BattleRuntimeDebug] No BattleManager found in scene.");
            return;
        }

        var orchestratorField = typeof(BattleManager).GetField("orchestrator",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        orchestrator = orchestratorField?.GetValue(battleManager) as BattleOrchestrator;
    }

    private void OnGUI()
    {
        if (battleManager == null) return;
        if (buttonStyle == null) SetupStyles();

        DrawAttackButton();
        DrawEnemyTurnButton();

        if (showStatusOverlay) DrawStatusOverlay();
    }

    private void SetupStyles()
    {
        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            normal = { textColor = Color.white }
        };

        headerStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.cyan }
        };
    }

    private void DrawAttackButton()
    {
        float x = Screen.width - buttonSize.x - margin;
        float y = margin;
        Rect rect = new Rect(x, y, buttonSize.x, buttonSize.y);

        if (GUI.Button(rect, "Attack", buttonStyle))
        {
            Debug.Log("[BattleRuntimeDebug] Manual attack trigger pressed.");
            battleManager.OnAttackButton();
        }
    }

    private void DrawEnemyTurnButton()
    {
        float x = Screen.width - buttonSize.x - margin;
        float y = margin + buttonSize.y + 5f;
        Rect rect = new Rect(x, y, buttonSize.x, buttonSize.y);

        if (GUI.Button(rect, "Enemy Turn", buttonStyle))
        {
            if (orchestrator == null)
            {
                Debug.LogWarning("[BattleRuntimeDebug] No orchestrator reference.");
                return;
            }

            Debug.Log("[BattleRuntimeDebug] Forcing enemy turn manually...");
            var method = typeof(BattleOrchestrator).GetMethod("BeginEnemyTurn",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (method != null)
            {
                method.Invoke(orchestrator, null);
            }
            else
            {
                Debug.LogWarning("[BattleRuntimeDebug] Could not find BeginEnemyTurn() method in orchestrator.");
            }
        }
    }

    private void DrawStatusOverlay()
    {
        if (orchestrator == null) return;

        float width = 240f;
        float height = 100f;
        float x = Screen.width - width - margin;
        float y = Screen.height - height - margin;
        Rect box = new Rect(x, y, width, height);

        GUI.Box(box, GUIContent.none);

        GUILayout.BeginArea(box);
        GUILayout.Label("Battle State", headerStyle);
        GUILayout.Space(5);

        bool isBusy = orchestrator.IsBusy;
        GUILayout.Label($"IsBusy: {(isBusy ? "TRUE" : "false")}", Colorize(isBusy));
        GUILayout.Label($"CanPlayerAct: {orchestrator.CanPlayerAct}", Colorize(orchestrator.CanPlayerAct));
        GUILayout.Label($"BattleOver: {orchestrator.BattleOver}", Colorize(orchestrator.BattleOver));

        GUILayout.EndArea();
    }

    private GUIStyle Colorize(bool state)
    {
        var style = new GUIStyle(labelStyle);
        style.normal.textColor = state ? Color.green : Color.red;
        return style;
    }
}




