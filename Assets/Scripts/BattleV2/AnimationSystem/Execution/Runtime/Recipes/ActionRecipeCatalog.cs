using System;
using System.Collections.Generic;

namespace BattleV2.AnimationSystem.Execution.Runtime.Recipes
{
    /// <summary>
    /// Lightweight in-memory registry for action recipes. Intended to be populated from code, ScriptableObjects
    /// or serialized data sources before delegating execution to the <see cref="StepScheduler"/>.
    /// </summary>
    public sealed class ActionRecipeCatalog
    {
        private readonly Dictionary<string, ActionRecipe> recipes = new Dictionary<string, ActionRecipe>(StringComparer.OrdinalIgnoreCase);

        public int Count => recipes.Count;

        public IEnumerable<ActionRecipe> Recipes => recipes.Values;

        public void Clear() => recipes.Clear();

        public void Register(ActionRecipe recipe)
        {
            if (recipe == null || string.IsNullOrWhiteSpace(recipe.Id))
            {
                return;
            }

            recipes[recipe.Id] = recipe;
        }

        public void Register(ActionRecipeBuilder.ActionRecipeDefinition definition, ActionRecipeBuilder builder)
        {
            if (definition == null)
            {
                return;
            }

            var resolver = builder ?? throw new ArgumentNullException(nameof(builder));
            var recipe = resolver.Build(definition);
            Register(recipe);
        }

        public void RegisterRange(IEnumerable<ActionRecipe> items)
        {
            if (items == null)
            {
                return;
            }

            foreach (var recipe in items)
            {
                Register(recipe);
            }
        }

        public bool TryGet(string id, out ActionRecipe recipe)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                recipe = null;
                return false;
            }

            return recipes.TryGetValue(id, out recipe);
        }

        public bool Remove(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            return recipes.Remove(id);
        }

        public bool TryResolveRecipe(string id, out ActionRecipe recipe)
        {
            if (string.IsNullOrEmpty(id) || !recipes.TryGetValue(id, out recipe))
            {
                recipe = null;
                return false;
            }

            return true;
        }
    }
}
