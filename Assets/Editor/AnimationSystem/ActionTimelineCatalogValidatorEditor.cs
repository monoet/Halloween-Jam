using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using BattleV2.AnimationSystem.Catalog;
using BattleV2.AnimationSystem.Validation;

namespace BattleV2.AnimationSystem.Editor
{
    public static class ActionTimelineCatalogValidatorEditor
    {
        [MenuItem("Battle/Animation/Validate Action Timeline Catalog")]
        public static void ValidateCatalog()
        {
            var catalogs = CollectCatalogsFromSelection();
            if (catalogs.Count == 0)
            {
                Debug.LogWarning("[ActionTimelineCatalogValidator] Selecciona al menos un ActionTimelineCatalog para validar.");
                return;
            }

            int totalTimelines = 0;
            int totalErrors = 0;
            int totalWarnings = 0;

            foreach (var catalog in catalogs)
            {
                catalog.ForceRebuild();
                var timelines = catalog.Timelines;
                if (timelines == null || timelines.Count == 0)
                {
                    Debug.LogWarning($"[ActionTimelineCatalogValidator] '{catalog.name}' esta vacio.");
                    continue;
                }

                int catalogErrors = 0;
                int catalogWarnings = 0;

                foreach (var timeline in timelines)
                {
                    var result = ActionTimelineValidator.Validate(timeline);
                    catalogErrors += result.ErrorCount;
                    catalogWarnings += result.WarningCount;

                    if (result.Errors != null)
                    {
                        foreach (var error in result.Errors)
                        {
                            Debug.LogError($"[ActionTimelineCatalogValidator] {catalog.name}/{timeline.ActionId}: {error}");
                        }
                    }

                    if (result.Warnings != null)
                    {
                        foreach (var warning in result.Warnings)
                        {
                            Debug.LogWarning($"[ActionTimelineCatalogValidator] {catalog.name}/{timeline.ActionId}: {warning}");
                        }
                    }
                }

                totalTimelines += timelines.Count;
                totalErrors += catalogErrors;
                totalWarnings += catalogWarnings;

                if (catalogErrors == 0 && catalogWarnings == 0)
                {
                    Debug.Log($"[ActionTimelineCatalogValidator] '{catalog.name}' validacion exitosa ({timelines.Count} timelines, sin hallazgos).");
                }
                else
                {
                    Debug.Log($"[ActionTimelineCatalogValidator] '{catalog.name}' resumen -> {timelines.Count} timelines | {catalogErrors} errores | {catalogWarnings} warnings.");
                }
            }

            Debug.Log($"[ActionTimelineCatalogValidator] Resultado global -> Catalogos: {catalogs.Count}, Timelines Analizados: {totalTimelines}, Errores: {totalErrors}, Warnings: {totalWarnings}.");
        }

        private static HashSet<ActionTimelineCatalog> CollectCatalogsFromSelection()
        {
            var catalogs = new HashSet<ActionTimelineCatalog>();
            foreach (var obj in Selection.objects)
            {
                if (obj is ActionTimelineCatalog catalog)
                {
                    catalogs.Add(catalog);
                }
            }

            if (catalogs.Count == 0 && Selection.activeObject is ActionTimelineCatalog active)
            {
                catalogs.Add(active);
            }

            return catalogs;
        }
    }
}
