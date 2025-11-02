using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BattleV2.EditorTools
{
    internal static class ScriptableObjectInventory
    {
        private const string OutputRelativePath = "Logs/ScriptableObjectInventory.md";

        [MenuItem("Battle/Tools/Generate ScriptableObject Inventory")]
        [MenuItem("Tools/Battle/Generate ScriptableObject Inventory")]
        private static void Generate()
        {
            var assetGuids = AssetDatabase.FindAssets("t:ScriptableObject");
            Array.Sort(assetGuids, StringComparer.Ordinal);

            var rows = new List<Row>(assetGuids.Length);
            var byNamespace = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var guid in assetGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (asset == null)
                {
                    continue;
                }

                var type = asset.GetType();
                var menuName = ResolveMenuName(type);
                var namespaceKey = type.Namespace ?? "(global)";

                rows.Add(new Row(path, type.FullName, menuName));

                if (byNamespace.TryGetValue(namespaceKey, out var count))
                {
                    byNamespace[namespaceKey] = count + 1;
                }
                else
                {
                    byNamespace[namespaceKey] = 1;
                }
            }

            rows.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));

            var builder = new StringBuilder(1024 + rows.Count * 128);
            builder.AppendLine("# ScriptableObject Inventory");
            builder.AppendLine();
            builder.AppendLine($"Generado: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            builder.AppendLine($"Total assets: {rows.Count}");
            builder.AppendLine();

            builder.AppendLine("## Resumen por namespace");
            foreach (var pair in Sorted(byNamespace))
            {
                builder.AppendLine($"- `{pair.Key}`: {pair.Value}");
            }

            builder.AppendLine();
            builder.AppendLine("## Detalle");
            builder.AppendLine("| Path | Type | Menu |");
            builder.AppendLine("| --- | --- | --- |");
            foreach (var row in rows)
            {
                builder.Append("| `").Append(row.Path).Append("` | `")
                    .Append(row.TypeName).Append("` | ")
                    .Append(string.IsNullOrEmpty(row.MenuName) ? "(none)" : $"`{row.MenuName}`")
                    .AppendLine(" |");
            }

            var outputPath = Path.Combine(Application.dataPath, "..", OutputRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllText(outputPath, builder.ToString());

            Debug.Log($"[SO Inventory] Report generated at {outputPath}");
        }

        private static IEnumerable<KeyValuePair<string, int>> Sorted(Dictionary<string, int> source)
        {
            var list = new List<KeyValuePair<string, int>>(source);
            list.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return list;
        }

        private static string ResolveMenuName(Type type)
        {
            var attribute = type.GetCustomAttribute<CreateAssetMenuAttribute>();
            return attribute != null ? attribute.menuName : string.Empty;
        }

        private readonly struct Row
        {
            public Row(string path, string typeName, string menuName)
            {
                Path = path;
                TypeName = typeName;
                MenuName = menuName;
            }

            public string Path { get; }
            public string TypeName { get; }
            public string MenuName { get; }
        }
    }
}
