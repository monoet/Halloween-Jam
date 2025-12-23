using System.Collections.Generic;
using System.Linq;
using BattleV2.Actions;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CombatantActionLoadout))]
public sealed class CombatantActionLoadoutEditor : Editor
{
    private ActionCatalog cachedCatalog;
    private HashSet<string> cachedIds;
    private string catalogPath;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();

        ResolveCatalogIfNeeded();

        var loadout = (CombatantActionLoadout)target;
        var ids = loadout.ActionIds;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Validation", EditorStyles.boldLabel);

        if (cachedCatalog == null)
        {
            EditorGUILayout.HelpBox("No ActionCatalog asset found. Cannot validate action ids.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.LabelField("Catalog", catalogPath ?? cachedCatalog.name);

            if (ids == null || ids.Count == 0)
            {
                EditorGUILayout.HelpBox("Loadout is empty (no allowed actions).", MessageType.Warning);
            }
            else
            {
                var invalid = new List<string>();
                for (int i = 0; i < ids.Count; i++)
                {
                    var id = ids[i];
                    if (string.IsNullOrWhiteSpace(id) || cachedIds == null || !cachedIds.Contains(id))
                    {
                        invalid.Add(string.IsNullOrWhiteSpace(id) ? "(empty)" : id);
                    }
                }

                if (invalid.Count > 0)
                {
                    EditorGUILayout.HelpBox($"Invalid action ids: {string.Join(", ", invalid.Distinct())}", MessageType.Error);
                }
                else
                {
                    EditorGUILayout.HelpBox("All action ids exist in the catalog.", MessageType.Info);
                }
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void ResolveCatalogIfNeeded()
    {
        if (cachedCatalog != null && cachedIds != null)
        {
            return;
        }

        var guids = AssetDatabase.FindAssets("t:ActionCatalog");
        if (guids == null || guids.Length == 0)
        {
            cachedCatalog = null;
            cachedIds = null;
            return;
        }

        var path = AssetDatabase.GUIDToAssetPath(guids[0]);
        var catalog = AssetDatabase.LoadAssetAtPath<ActionCatalog>(path);
        if (catalog == null)
        {
            cachedCatalog = null;
            cachedIds = null;
            return;
        }

        catalogPath = path;
        cachedCatalog = catalog;

        cachedIds = new HashSet<string>();
        AddIds(catalog.Basic);
        AddIds(catalog.Magic);
        AddIds(catalog.Items);
    }

    private void AddIds(IReadOnlyList<BattleActionData> list)
    {
        if (list == null || cachedIds == null)
        {
            return;
        }

        for (int i = 0; i < list.Count; i++)
        {
            var data = list[i];
            if (data != null && !string.IsNullOrWhiteSpace(data.id))
            {
                cachedIds.Add(data.id);
            }
        }
    }
}

