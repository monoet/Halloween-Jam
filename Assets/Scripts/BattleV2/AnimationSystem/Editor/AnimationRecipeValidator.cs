
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using BattleV2.AnimationSystem.Execution.Runtime;
using BattleV2.AnimationSystem.Execution.Runtime.Recipes;
using BattleV2.AnimationSystem.Runtime;
using BattleV2.Orchestration.Runtime;
using UnityEditor;
using UnityEngine;

namespace BattleV2.AnimationSystem.Editor
{
    public static class AnimationRecipeValidator
    {
        private const string MenuPath = "Battle/Animation/Validate Recipes and Bindings";
        private static readonly IReadOnlyList<AnimationClipBinding> EmptyClipBindings = Array.Empty<AnimationClipBinding>();
        private static readonly IReadOnlyList<FlipbookBinding> EmptyFlipbookBindings = Array.Empty<FlipbookBinding>();
        private static readonly IReadOnlyList<TransformTweenBinding> EmptyTweenBindings = Array.Empty<TransformTweenBinding>();

        [MenuItem(MenuPath)]
        public static void ValidateAll()
        {
            var issues = new List<string>();
            ValidateCharacterAnimationSets(issues);
            ValidateRegisteredRecipes(issues);

            if (issues.Count == 0)
            {
                Debug.Log("[AnimationRecipeValidator] No issues found.");
                return;
            }

            foreach (var issue in issues)
            {
                Debug.LogWarning($"[AnimationRecipeValidator] {issue}");
            }
        }

        private static void ValidateCharacterAnimationSets(List<string> issues)
        {
            var guids = AssetDatabase.FindAssets("t:CharacterAnimationSet");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var set = AssetDatabase.LoadAssetAtPath<CharacterAnimationSet>(path);
                if (set == null)
                {
                    continue;
                }

                set.WarmUpCache();
                ValidateClipBindings(set, path, issues);
                ValidateFlipbookBindings(set, path, issues);
                ValidateTweenBindings(set, path, issues);
            }
        }

        private static void ValidateClipBindings(CharacterAnimationSet set, string assetPath, List<string> issues)
        {
            var seen = new HashSet<string>();
            var bindings = set.ClipBindings ?? EmptyClipBindings;
            foreach (var binding in bindings)
            {
                if (string.IsNullOrWhiteSpace(binding.Id))
                {
                    issues.Add($"[{assetPath}] Clip binding with empty id.");
                    continue;
                }

                if (binding.Clip == null)
                {
                    issues.Add($"[{assetPath}] Clip binding '{binding.Id}' has null AnimationClip.");
                }

                if (!seen.Add(binding.Id))
                {
                    issues.Add($"[{assetPath}] Duplicate clip binding id '{binding.Id}'.");
                }
            }
        }

        private static void ValidateFlipbookBindings(CharacterAnimationSet set, string assetPath, List<string> issues)
        {
            var seen = new HashSet<string>();
            var bindings = set.FlipbookBindings ?? EmptyFlipbookBindings;
            foreach (var binding in bindings)
            {
                if (string.IsNullOrWhiteSpace(binding.Id))
                {
                    issues.Add($"[{assetPath}] Flipbook binding with empty id.");
                    continue;
                }

                if (!seen.Add(binding.Id))
                {
                    issues.Add($"[{assetPath}] Duplicate flipbook binding id '{binding.Id}'.");
                }

                if (binding.Frames == null || binding.Frames.Length == 0)
                {
                    issues.Add($"[{assetPath}] Flipbook '{binding.Id}' has no frames.");
                }
            }
        }

        private static void ValidateTweenBindings(CharacterAnimationSet set, string assetPath, List<string> issues)
        {
            var seen = new HashSet<string>();
            var bindings = set.TweenBindings ?? EmptyTweenBindings;
            foreach (var binding in bindings)
            {
                if (string.IsNullOrWhiteSpace(binding.Id))
                {
                    issues.Add($"[{assetPath}] Tween binding with empty id.");
                    continue;
                }

                if (!seen.Add(binding.Id))
                {
                    issues.Add($"[{assetPath}] Duplicate tween binding id '{binding.Id}'.");
                }

                if (!binding.Tween.IsValid)
                {
                    issues.Add($"[{assetPath}] Tween '{binding.Id}' is invalid (missing duration or targets).");
                }
            }
        }

        private static void ValidateRegisteredRecipes(List<string> issues)
        {
            var installer = AnimationSystem.Runtime.AnimationSystemInstaller.Current;
            if (installer == null)
            {
                issues.Add("AnimationSystemInstaller not present in the scene. Cannot validate registered recipes.");
                return;
            }

            var stepScheduler = installer.StepScheduler;
            if (stepScheduler == null)
            {
                issues.Add("StepScheduler instance not initialised on installer.");
                return;
            }

            if (!stepScheduler.TryGetRecipe(SampleActionRecipes.BasicAttack.Id, out _))
            {
                issues.Add("Sample recipe 'BasicAttack' is not registered.");
            }

            if (!stepScheduler.TryGetRecipe(SampleActionRecipes.UseItem.Id, out _))
            {
                issues.Add("Sample recipe 'UseItem' is not registered.");
            }
        }
    }
}
#endif
