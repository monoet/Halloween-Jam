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
            var catalog = Selection.activeObject as ActionTimelineCatalog;
            if (catalog == null)
            {
                Debug.LogWarning("[ActionTimelineCatalogValidator] Selecciona un ActionTimelineCatalog para validar.");
                return;
            }

            catalog.Initialize();
            var timelines = catalog.Timelines;
            if (timelines == null || timelines.Count == 0)
            {
                Debug.LogWarning("[ActionTimelineCatalogValidator] Catálogo vacío.");
                return;
            }

            var errors = new List<string>();
            var warnings = new List<string>();

            foreach (var timeline in timelines)
            {
                var result = ActionTimelineValidator.Validate(timeline);
                if (result.Errors != null)
                {
                    errors.AddRange(result.Errors);
                }

                if (result.Warnings != null)
                {
                    warnings.AddRange(result.Warnings);
                }
            }

            if (errors.Count == 0 && warnings.Count == 0)
            {
                Debug.Log("[ActionTimelineCatalogValidator] Validación exitosa sin observaciones.");
                return;
            }

            foreach (var error in errors)
            {
                Debug.LogError($"[ActionTimelineCatalogValidator] {error}");
            }

            foreach (var warning in warnings)
            {
                Debug.LogWarning($"[ActionTimelineCatalogValidator] {warning}");
            }
        }
    }
}
