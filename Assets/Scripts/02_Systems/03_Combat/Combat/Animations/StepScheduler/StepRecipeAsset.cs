using System;
using System.Collections.Generic;
using BattleV2.AnimationSystem.Execution.Runtime;
using BattleV2.AnimationSystem.Execution.Runtime.Recipes;
using UnityEngine;

namespace HalloweenJam.Combat.Animations.StepScheduler
{
    /// <summary>
    /// ScriptableObject representation of an <see cref="ActionRecipe"/> so designers can author step-scheduler flows.
    /// </summary>
    [CreateAssetMenu(menuName = "Battle/Animation/Step Recipe")]
    public sealed class StepRecipeAsset : ScriptableObject
    {
        [SerializeField] private string recipeId;
        [SerializeField] private StepGroup[] groups = Array.Empty<StepGroup>();

        public string RecipeId => recipeId;

        public bool TryBuild(out ActionRecipe recipe)
        {
            recipe = BuildInternal();
            return recipe != null && !recipe.IsEmpty;
        }

        public ActionRecipe BuildRecipe()
        {
            return BuildInternal() ?? ActionRecipe.Empty;
        }

        private ActionRecipe BuildInternal()
        {
            if (string.IsNullOrWhiteSpace(recipeId) || groups == null || groups.Length == 0)
            {
                return null;
            }

            var runtimeGroups = new List<ActionStepGroup>();
            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i] == null)
                {
                    continue;
                }

                if (groups[i].TryBuild(out var group))
                {
                    runtimeGroups.Add(group);
                }
            }

            if (runtimeGroups.Count == 0)
            {
                return null;
            }

            return new ActionRecipe(recipeId, runtimeGroups);
        }

        [Serializable]
        private class StepGroup
        {
            [SerializeField] private string groupId;
            [SerializeField] private StepGroupExecutionMode executionMode = StepGroupExecutionMode.Sequential;
            [SerializeField] private StepGroupJoinPolicy joinPolicy = StepGroupJoinPolicy.Any;
            [SerializeField] private float timeoutSeconds;
            [SerializeField] private StepDefinition[] steps = Array.Empty<StepDefinition>();

            public bool TryBuild(out ActionStepGroup group)
            {
                group = null;
                if (steps == null || steps.Length == 0)
                {
                    return false;
                }

                var runtimeSteps = new List<ActionStep>();
                for (int i = 0; i < steps.Length; i++)
                {
                    if (steps[i] == null)
                    {
                        continue;
                    }

                    if (steps[i].TryBuild(out var step))
                    {
                        runtimeSteps.Add(step);
                    }
                }

                if (runtimeSteps.Count == 0)
                {
                    return false;
                }

                group = new ActionStepGroup(
                    string.IsNullOrWhiteSpace(groupId) ? null : groupId,
                    runtimeSteps,
                    executionMode,
                    joinPolicy,
                    Mathf.Max(0f, timeoutSeconds));
                return true;
            }
        }

        [Serializable]
        private class StepDefinition
        {
            [SerializeField] private string stepId;
            [SerializeField] private string executorId;
            [SerializeField] private string bindingId;
            [SerializeField] private StepConflictPolicy conflictPolicy = StepConflictPolicy.WaitForCompletion;
            [SerializeField] private bool overrideConflictPolicy;
            [SerializeField] private float delaySeconds;
            [SerializeField] private Parameter[] parameters = Array.Empty<Parameter>();

            public bool TryBuild(out ActionStep step)
            {
                step = default;
                if (string.IsNullOrWhiteSpace(executorId))
                {
                    return false;
                }

                var parameterMap = BuildParameterDictionary();
                var parameterWrapper = new ActionStepParameters(parameterMap);

                step = new ActionStep(
                    executorId,
                    string.IsNullOrWhiteSpace(bindingId) ? null : bindingId,
                    parameterWrapper,
                    conflictPolicy,
                    string.IsNullOrWhiteSpace(stepId) ? null : stepId,
                    Mathf.Max(0f, delaySeconds),
                    overrideConflictPolicy);
                return true;
            }

            private IReadOnlyDictionary<string, string> BuildParameterDictionary()
            {
                if (parameters == null || parameters.Length == 0)
                {
                    return null;
                }

                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < parameters.Length; i++)
                {
                    if (parameters[i] == null)
                    {
                        continue;
                    }

                    var key = parameters[i].Key;
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        continue;
                    }

                    map[key] = parameters[i].Value;
                }

                return map.Count == 0 ? null : map;
            }
        }

        [Serializable]
        private class Parameter
        {
            [SerializeField] private string key;
            [SerializeField] private string value;

            public string Key => key;
            public string Value => value;
        }
    }
}
