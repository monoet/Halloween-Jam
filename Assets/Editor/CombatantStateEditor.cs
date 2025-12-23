using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CombatantState))]
public sealed class CombatantStateEditor : Editor
{
    private SerializedProperty actionLoadout;
    private SerializedProperty allowLegacyActionFallback;
    private SerializedProperty allowedActionIds;

    private bool showLegacyList;
    private bool showEffective;

    private void OnEnable()
    {
        actionLoadout = serializedObject.FindProperty("actionLoadout");
        allowLegacyActionFallback = serializedObject.FindProperty("allowLegacyActionFallback");
        allowedActionIds = serializedObject.FindProperty("allowedActionIds");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Draw everything except our "actions" section, then draw it in a dedicated block.
        DrawPropertiesExcluding(
            serializedObject,
            "m_Script",
            "actionLoadout",
            "allowLegacyActionFallback",
            "allowedActionIds");

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Actions (BattleV2)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(actionLoadout, new GUIContent("Action Loadout (Asset)"));
        EditorGUILayout.PropertyField(allowLegacyActionFallback, new GUIContent("Allow Legacy Fallback"));

        showLegacyList = EditorGUILayout.Foldout(showLegacyList, "Legacy Allowed Action Ids (Prefab Override)", true);
        if (showLegacyList)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(allowedActionIds, new GUIContent("Allowed Action Ids (Legacy)"), includeChildren: true);
            }
        }

        var state = target as CombatantState;
        if (state != null)
        {
            bool hasLoadoutRef = state.ActionLoadout != null;
            bool hasAssetIds = state.ActionLoadout != null &&
                               state.ActionLoadout.ActionIds != null &&
                               state.ActionLoadout.ActionIds.Count > 0;
            bool hasLegacyIds = allowedActionIds != null && allowedActionIds.isArray && allowedActionIds.arraySize > 0;
            bool allowFallback = state.AllowLegacyActionFallback;

            if (hasLoadoutRef && hasLegacyIds && hasAssetIds)
            {
                EditorGUILayout.HelpBox("Legacy Allowed Action Ids is ignored because Action Loadout has ids.", MessageType.Error);
            }
            else if (hasLoadoutRef && hasLegacyIds && !hasAssetIds && !allowFallback)
            {
                EditorGUILayout.HelpBox("Action Loadout is assigned but empty and legacy fallback is disabled: this combatant will have no allowed actions (empty = none).", MessageType.Warning);
            }
            else if (hasLoadoutRef && !hasAssetIds && allowFallback)
            {
                EditorGUILayout.HelpBox("Action Loadout is assigned but empty: falling back to Legacy Allowed Action Ids.", MessageType.Warning);
            }

            showEffective = EditorGUILayout.Foldout(showEffective, "Effective Allowed Action Ids (Read-only)", true);
            if (showEffective)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    var ids = state.AllowedActionIds;
                    string sourceLabel = hasAssetIds ? "Source: Action Loadout" : (hasLegacyIds ? "Source: Legacy List" : "Source: (none = catalog)");
                    EditorGUILayout.LabelField(sourceLabel);

                    if (ids == null || ids.Count == 0)
                    {
                        EditorGUILayout.LabelField("[]");
                    }
                    else
                    {
                        for (int i = 0; i < ids.Count; i++)
                        {
                            EditorGUILayout.SelectableLabel(ids[i] ?? "(null)", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                        }
                    }
                }
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
