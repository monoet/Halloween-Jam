using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Scans project assets for potential external (absolute) paths to help migration/backups.
/// Writes a short report in the project root.
/// </summary>
public static class ExternalReferenceScanner
{
    private static readonly string[] RootsToScan =
    {
        "Assets",
        "ProjectSettings",
        "Packages"
    };

    private static readonly HashSet<string> Extensions = new HashSet<string>
    {
        ".asset", ".meta", ".prefab", ".unity", ".controller", ".playable", ".anim", ".overrideController", ".mat"
    };

    // Rough detector for absolute paths (Windows drive letters, UNC, or common POSIX roots).
    private static readonly Regex AbsolutePathRegex = new Regex(
        @"([A-Za-z]:[\\/])|(\\\\[^\\s/]+[\\/])|(/(Users|home|mnt|Volumes|var|opt)/)",
        RegexOptions.Compiled);

    [MenuItem("Tools/Scan External References")]
    public static void Scan()
    {
        string projectRoot = Application.dataPath.Replace("/Assets", string.Empty);
        var files = EnumerateFiles(projectRoot);

        var matches = new List<string>(64);

        foreach (var file in files)
        {
            int lineNumber = 0;
            foreach (var line in File.ReadLines(file))
            {
                lineNumber++;
                if (AbsolutePathRegex.IsMatch(line))
                {
                    string trimmed = line.Trim();
                    matches.Add($"{file}:{lineNumber} :: {trimmed}");
                }
            }
        }

        if (matches.Count == 0)
        {
            Debug.Log("No se encontraron rutas externas. Todo dentro del proyecto.");
            return;
        }

        string reportPath = Path.Combine(projectRoot, "ExternalReferenceReport.txt");
        File.WriteAllLines(reportPath, matches, Encoding.UTF8);

        Debug.LogWarning($"Posibles referencias externas encontradas: {matches.Count}. Reporte: {reportPath}");
        foreach (var match in matches.Take(50))
        {
            Debug.LogWarning(match);
        }
        if (matches.Count > 50)
        {
            Debug.LogWarning($"Se truncaron logs ({matches.Count - 50} adicionales). Revisa el archivo completo.");
        }
    }

    private static IEnumerable<string> EnumerateFiles(string projectRoot)
    {
        foreach (var root in RootsToScan)
        {
            string fullRoot = Path.Combine(projectRoot, root);
            if (!Directory.Exists(fullRoot))
            {
                continue;
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(fullRoot, "*.*", SearchOption.AllDirectories);
            }
            catch (IOException)
            {
                continue;
            }

            foreach (var file in files)
            {
                if (Extensions.Contains(Path.GetExtension(file)))
                {
                    yield return file;
                }
            }
        }
    }
}
