using UnityEditor;
using UnityEngine;
using BattleV2.Orchestration;

[CustomEditor(typeof(BattleManagerV2))]
public sealed class BattleManagerV2Editor : Editor
{
    private SerializedProperty actionCatalog;
    private SerializedProperty config;

    private SerializedProperty playerLoadout;
    private SerializedProperty playerPartyLoadout;
    private SerializedProperty playerSpawnPoint;
    private SerializedProperty playerSpawnPoints;
    private SerializedProperty autoSpawnPlayer;

    private SerializedProperty enemyLoadout;
    private SerializedProperty enemyEncounterLoadout;
    private SerializedProperty enemySpawnPoint;
    private SerializedProperty enemySpawnPoints;
    private SerializedProperty autoSpawnEnemy;

    private SerializedProperty animationSystemInstaller;
    private SerializedProperty timingConfig;
    private SerializedProperty hudManager;
    private SerializedProperty inputProviderAsset;
    private SerializedProperty inputProviderComponent;

    private SerializedProperty preActionDelaySeconds;
    private SerializedProperty enableDebugLogs;

    private bool showSystems;
    private bool showDebug;

    private void OnEnable()
    {
        actionCatalog = serializedObject.FindProperty("actionCatalog");
        config = serializedObject.FindProperty("config");

        playerLoadout = serializedObject.FindProperty("playerLoadout");
        playerPartyLoadout = serializedObject.FindProperty("playerPartyLoadout");
        playerSpawnPoint = serializedObject.FindProperty("playerSpawnPoint");
        playerSpawnPoints = serializedObject.FindProperty("playerSpawnPoints");
        autoSpawnPlayer = serializedObject.FindProperty("autoSpawnPlayer");

        enemyLoadout = serializedObject.FindProperty("enemyLoadout");
        enemyEncounterLoadout = serializedObject.FindProperty("enemyEncounterLoadout");
        enemySpawnPoint = serializedObject.FindProperty("enemySpawnPoint");
        enemySpawnPoints = serializedObject.FindProperty("enemySpawnPoints");
        autoSpawnEnemy = serializedObject.FindProperty("autoSpawnEnemy");

        animationSystemInstaller = serializedObject.FindProperty("animationSystemInstaller");
        timingConfig = serializedObject.FindProperty("timingConfig");
        hudManager = serializedObject.FindProperty("hudManager");
        inputProviderAsset = serializedObject.FindProperty("inputProviderAsset");
        inputProviderComponent = serializedObject.FindProperty("inputProviderComponent");

        preActionDelaySeconds = serializedObject.FindProperty("preActionDelaySeconds");
        enableDebugLogs = serializedObject.FindProperty("enableDebugLogs");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawGameplayConfig();
        EditorGUILayout.Space();
        DrawSystems();
        EditorGUILayout.Space();
        DrawAdvanced();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawGameplayConfig()
    {
        EditorGUILayout.LabelField("Gameplay Config", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(actionCatalog, new GUIContent("Action Catalog"));
        EditorGUILayout.PropertyField(config, new GUIContent("Battle Config"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Player Party Setup", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(autoSpawnPlayer, new GUIContent("Auto Spawn Player"));
        EditorGUILayout.PropertyField(playerLoadout, new GUIContent("Player Loadout"));
        EditorGUILayout.PropertyField(playerPartyLoadout, new GUIContent("Player Party Loadout"));
        EditorGUILayout.PropertyField(playerSpawnPoint, new GUIContent("Player Spawn Point"));
        EditorGUILayout.PropertyField(playerSpawnPoints, new GUIContent("Player Spawn Points"), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Enemy Encounter Setup", EditorStyles.miniBoldLabel);
        EditorGUILayout.PropertyField(autoSpawnEnemy, new GUIContent("Auto Spawn Enemy"));
        EditorGUILayout.PropertyField(enemyEncounterLoadout, new GUIContent("Enemy Encounter Loadout"));
        EditorGUILayout.PropertyField(enemyLoadout, new GUIContent("Enemy Loadout"));
        EditorGUILayout.PropertyField(enemySpawnPoint, new GUIContent("Enemy Spawn Point"));
        EditorGUILayout.PropertyField(enemySpawnPoints, new GUIContent("Enemy Spawn Points"), true);
    }

    private void DrawSystems()
    {
        showSystems = EditorGUILayout.Foldout(showSystems, "Systems (rarely changed)", true);
        if (!showSystems)
        {
            return;
        }

        using (new EditorGUI.IndentLevelScope())
        {
            EditorGUILayout.PropertyField(animationSystemInstaller, new GUIContent("Animation System Installer"));
            EditorGUILayout.PropertyField(timingConfig, new GUIContent("Timing Config"));
            EditorGUILayout.PropertyField(hudManager, new GUIContent("HUD Manager"));
            EditorGUILayout.PropertyField(inputProviderAsset, new GUIContent("Input Provider Asset"));
            EditorGUILayout.PropertyField(inputProviderComponent, new GUIContent("Input Provider Component"));
        }
    }

    private void DrawAdvanced()
    {
        showDebug = EditorGUILayout.Foldout(showDebug, "Advanced / Debug", true);
        if (!showDebug)
        {
            return;
        }

        using (new EditorGUI.IndentLevelScope())
        {
            EditorGUILayout.LabelField("Pipeline Tuning", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(preActionDelaySeconds, new GUIContent("Pre Action Delay Seconds"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Logging", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(enableDebugLogs, new GUIContent("Enable Debug Logs"));
        }
    }
}
