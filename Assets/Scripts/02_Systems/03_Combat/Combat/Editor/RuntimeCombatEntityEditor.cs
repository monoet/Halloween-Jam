using System;
using HalloweenJam.Combat;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RuntimeCombatEntity))]
public sealed class RuntimeCombatEntityEditor : Editor
{
    private bool showBattleV2Actions = true;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var entity = (RuntimeCombatEntity)target;

        EditorGUILayout.Space(8);
        showBattleV2Actions = EditorGUILayout.Foldout(showBattleV2Actions, "Available Actions (Runtime / BattleV2)", true);
        if (!showBattleV2Actions)
        {
            return;
        }

        using (new EditorGUI.IndentLevelScope())
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Visible en Play Mode. No se configura aquí.", MessageType.Info);
                return;
            }

            var ids = entity.CurrentAvailableActionIds;
            if (ids == null || ids.Count == 0)
            {
                EditorGUILayout.LabelField("Action Ids", "[]");
            }
            else
            {
                for (int i = 0; i < ids.Count; i++)
                {
                    EditorGUILayout.SelectableLabel(ids[i] ?? "(null)", EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                }
            }

            if (GUILayout.Button("Recompute (Debug)"))
            {
                entity.RecomputeAvailableActions("InspectorButton");
            }
        }
    }
}

