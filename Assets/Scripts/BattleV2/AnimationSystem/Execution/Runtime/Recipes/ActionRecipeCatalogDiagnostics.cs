using System.Linq;
using UnityEngine;
using BattleV2.AnimationSystem.Execution.Runtime.Executors;

namespace BattleV2.AnimationSystem.Execution.Runtime.Recipes
{
    internal static class ActionRecipeCatalogDiagnostics
    {
        public static void ValidatePilotRecipes(ActionRecipeCatalog catalog)
        {
            if (catalog == null)
            {
                return;
            }

            ValidateBasicAttackLight(catalog);
            ValidateContainsStep(catalog, PilotActionRecipes.BasicAttackSuccessId, "damage.apply");
            ValidateContainsStep(catalog, PilotActionRecipes.BasicAttackMediocreId, "fallback");
            ValidateContainsStep(catalog, PilotActionRecipes.UseItemId, AnimatorClipExecutor.ExecutorId);
        }

        private static void ValidateBasicAttackLight(ActionRecipeCatalog catalog)
        {
            if (!catalog.TryGet(PilotActionRecipes.BasicAttackLightId, out var recipe))
            {
                Debug.LogWarning($"[ActionRecipeCatalog] Missing pilot recipe '{PilotActionRecipes.BasicAttackLightId}'.");
                return;
            }

            bool hasIntro = recipe.Groups.Any(g => g.Id == $"{PilotActionRecipes.BasicAttackLightId}/Intro");
            bool hasSuccess = recipe.Groups.Any(g => g.Id == PilotActionRecipes.BasicAttackSuccessId);
            bool hasFail = recipe.Groups.Any(g => g.Id == PilotActionRecipes.BasicAttackMediocreId);

            if (!(hasIntro && hasSuccess && hasFail))
            {
                Debug.LogWarning($"[ActionRecipeCatalog] Recipe '{PilotActionRecipes.BasicAttackLightId}' is missing expected groups (Intro={hasIntro}, Success={hasSuccess}, Mediocre={hasFail}).");
            }
        }

        private static void ValidateContainsStep(ActionRecipeCatalog catalog, string recipeId, string executorId)
        {
            if (!catalog.TryGet(recipeId, out var recipe))
            {
                Debug.LogWarning($"[ActionRecipeCatalog] Missing pilot recipe '{recipeId}'.");
                return;
            }

            bool found = recipe.Groups.Any(group => group.Steps.Any(step => string.Equals(step.ExecutorId, executorId, System.StringComparison.OrdinalIgnoreCase)));
            if (!found)
            {
                Debug.LogWarning($"[ActionRecipeCatalog] Recipe '{recipeId}' does not contain a step for executor '{executorId}'.");
            }
        }
    }
}
