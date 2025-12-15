using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CombatantState))]
public sealed class CombatantStateEditor : Editor
{
    private SerializedProperty characterRuntime;
    private SerializedProperty faction;
    private SerializedProperty teamId;
    private SerializedProperty displayName;
    private SerializedProperty audioSignatureId;
    private SerializedProperty initializeOnAwake;

    private SerializedProperty maxHP;
    private SerializedProperty currentHP;
    private SerializedProperty maxSP;
    private SerializedProperty currentSP;
    private SerializedProperty fallbackMaxHP;
    private SerializedProperty maxCP;
    private SerializedProperty currentCP;
    private SerializedProperty activeMark;

    private SerializedProperty allowedActionIds;
    private SerializedProperty enableDebugLogs;

    private bool showDebug;

    private void OnEnable()
    {
        characterRuntime = serializedObject.FindProperty("characterRuntime");
        faction = serializedObject.FindProperty("faction");
        teamId = serializedObject.FindProperty("teamId");
        displayName = serializedObject.FindProperty("displayName");
        audioSignatureId = serializedObject.FindProperty("audioSignatureId");
        initializeOnAwake = serializedObject.FindProperty("initializeOnAwake");

        maxHP = serializedObject.FindProperty("maxHP");
        currentHP = serializedObject.FindProperty("currentHP");
        maxSP = serializedObject.FindProperty("maxSP");
        currentSP = serializedObject.FindProperty("currentSP");
        fallbackMaxHP = serializedObject.FindProperty("fallbackMaxHP");
        maxCP = serializedObject.FindProperty("maxCP");
        currentCP = serializedObject.FindProperty("currentCP");
        activeMark = serializedObject.FindProperty("activeMark");

        allowedActionIds = serializedObject.FindProperty("allowedActionIds");
        enableDebugLogs = serializedObject.FindProperty("enableDebugLogs");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawIdentity();
        EditorGUILayout.Space();
        DrawVitals();
        EditorGUILayout.Space();
        DrawActions();
        EditorGUILayout.Space();
        DrawDebug();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawIdentity()
    {
        EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(characterRuntime, new GUIContent("Character Runtime"));
        EditorGUILayout.PropertyField(faction, new GUIContent("Faction"));

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(teamId, new GUIContent("Team Id (auto)"));
        }

        EditorGUILayout.PropertyField(displayName, new GUIContent("Display Name (override)"));
        EditorGUILayout.PropertyField(audioSignatureId, new GUIContent("Audio Signature Id"));
        EditorGUILayout.PropertyField(initializeOnAwake, new GUIContent("Initialize On Awake"));
    }

    private void DrawVitals()
    {
        EditorGUILayout.LabelField("Vitals", EditorStyles.boldLabel);

        EditorGUILayout.LabelField("HP", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(maxHP, new GUIContent("Max HP"));
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            EditorGUILayout.PropertyField(currentHP, new GUIContent("Current HP"));
        }

        EditorGUILayout.PropertyField(fallbackMaxHP, new GUIContent("Fallback Max HP"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("SP", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(maxSP, new GUIContent("Max SP"));
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            EditorGUILayout.PropertyField(currentSP, new GUIContent("Current SP"));
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("CP", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(maxCP, new GUIContent("Max CP"));
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            EditorGUILayout.PropertyField(currentCP, new GUIContent("Current CP"));
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Mark Slot", EditorStyles.miniBoldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(activeMark, new GUIContent("Active Mark"));
        }
    }

    private void DrawActions()
    {
        EditorGUILayout.LabelField("Actions & Behavior", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(allowedActionIds, new GUIContent("Allowed Action Ids"), true);
        EditorGUILayout.HelpBox("Legacy AttackStrategy is hidden (CombatV1 only). BattleV2 uses ActionCatalog/Loadouts.", MessageType.Info);
    }

    private void DrawDebug()
    {
        showDebug = EditorGUILayout.Foldout(showDebug, "Debug / Advanced", true);
        if (!showDebug)
        {
            return;
        }

        using (new EditorGUI.IndentLevelScope())
        {
            EditorGUILayout.PropertyField(enableDebugLogs, new GUIContent("Enable Debug Logs"));

            // StableId and runtime flags are read-only.
            var combatant = target as CombatantState;
            if (combatant != null)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.IntField("Stable Id", combatant.StableId);
                    EditorGUILayout.Toggle("Is Alive", combatant.IsAlive);
                    EditorGUILayout.Toggle("Is Downed", combatant.IsDowned);
                }
            }
        }
    }
}
