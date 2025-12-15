using System.IO;
using UnityEditor;
using UnityEngine;
using HalloweenJam.Combat.Animations.StepScheduler;
using BattleV2.AnimationSystem.Execution.Runtime;

public static class StepSchedulerV2RecipeGenerator
{
    private const string OutputFolder = "Assets/Resources/Battle/StepRecipesV2";

    [MenuItem("Battle/Animation/StepScheduler v2/Generate Locomotion Recipes (Resources)")]
    public static void Generate()
    {
        EnsureFolder(OutputFolder);

        CreateOrUpdateLocomotionRecipe("move_to_spotlight");
        CreateOrUpdateLocomotionRecipe("move_to_target");
        CreateOrUpdateLocomotionRecipe("return_home");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[StepSchedulerV2RecipeGenerator] Generated recipes under {OutputFolder}");
    }

    private static void CreateOrUpdateLocomotionRecipe(string recipeId)
    {
        var path = $"{OutputFolder}/{recipeId}.asset";
        var asset = AssetDatabase.LoadAssetAtPath<StepRecipeAsset>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<StepRecipeAsset>();
            AssetDatabase.CreateAsset(asset, path);
        }

        var serialized = new SerializedObject(asset);
        serialized.FindProperty("recipeId").stringValue = recipeId;

        var groups = serialized.FindProperty("groups");
        groups.arraySize = 1;

        var group = groups.GetArrayElementAtIndex(0);
        group.FindPropertyRelative("groupId").stringValue = recipeId;
        group.FindPropertyRelative("executionMode").enumValueIndex = (int)BattleV2.AnimationSystem.Execution.Runtime.StepGroupExecutionMode.Sequential;
        group.FindPropertyRelative("joinPolicy").enumValueIndex = (int)BattleV2.AnimationSystem.Execution.Runtime.StepGroupJoinPolicy.Any;
        group.FindPropertyRelative("timeoutSeconds").floatValue = 0f;

        var steps = group.FindPropertyRelative("steps");
        steps.arraySize = 1;
        var step = steps.GetArrayElementAtIndex(0);
        step.FindPropertyRelative("stepId").stringValue = "noop";
        step.FindPropertyRelative("executorId").stringValue = NoOpStepExecutor.ExecutorId;
        step.FindPropertyRelative("bindingId").stringValue = string.Empty;
        step.FindPropertyRelative("conflictPolicy").enumValueIndex = (int)BattleV2.AnimationSystem.Execution.Runtime.StepConflictPolicy.WaitForCompletion;
        step.FindPropertyRelative("overrideConflictPolicy").boolValue = false;
        step.FindPropertyRelative("delaySeconds").floatValue = 0f;
        step.FindPropertyRelative("parameters").arraySize = 0;

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        var parts = path.Split('/');
        var current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }
}
