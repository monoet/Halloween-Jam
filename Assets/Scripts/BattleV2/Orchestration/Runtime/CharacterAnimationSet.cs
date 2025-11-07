using System;
using System.Collections.Generic;
using UnityEngine;
using BattleV2.AnimationSystem.Runtime;
using BattleV2.AnimationSystem.Execution.Runtime;

namespace BattleV2.Orchestration.Runtime
{
    /// <summary>
    /// ScriptableObject that stores the animation bindings (clips, flipbooks, tweens) for a specific combatant prefab.
    /// </summary>
    [CreateAssetMenu(menuName = "Battle/Animation/Character Animation Set")]
    public sealed class CharacterAnimationSet : ScriptableObject, IAnimationBindingResolver
    {
        [SerializeField]
        private AnimationClipBinding[] clipBindings = System.Array.Empty<AnimationClipBinding>();

        [SerializeField]
        private FlipbookBinding[] flipbookBindings = System.Array.Empty<FlipbookBinding>();

        [SerializeField]
        private TransformTweenBinding[] tweenBindings = System.Array.Empty<TransformTweenBinding>();

        [NonSerialized] private Dictionary<string, AnimationClip> clipLookup;
        [NonSerialized] private Dictionary<string, FlipbookBinding> flipbookLookup;
        [NonSerialized] private Dictionary<string, TransformTween> tweenLookup;
        [NonSerialized] private Dictionary<string, TransformTweenProvider> tweenProviderLookup;
        [NonSerialized] private bool cacheDirty = true;

        public IReadOnlyList<AnimationClipBinding> ClipBindings => clipBindings ?? System.Array.Empty<AnimationClipBinding>();

        public IReadOnlyList<FlipbookBinding> FlipbookBindings => flipbookBindings ?? System.Array.Empty<FlipbookBinding>();

        public IReadOnlyList<TransformTweenBinding> TweenBindings => tweenBindings ?? System.Array.Empty<TransformTweenBinding>();

        /// <summary>Legacy accessor used by components that still expect clip entries.</summary>
        public IReadOnlyList<AnimationClipBinding> Entries => ClipBindings;

        private void OnEnable()
        {
            WarmUpCache();
        }

        private void OnValidate()
        {
            cacheDirty = true;
        }

        public void WarmUpCache()
        {
            EnsureCachesBuilt();
        }

        public bool TryGetClip(string id, out AnimationClip clip)
        {
            clip = null;
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            EnsureCachesBuilt();
            return clipLookup != null && clipLookup.TryGetValue(id, out clip) && clip != null;
        }

        public bool TryGetFlipbook(string id, out FlipbookBinding binding)
        {
            binding = default;
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            EnsureCachesBuilt();
            return flipbookLookup != null && flipbookLookup.TryGetValue(id, out binding);
        }

        public bool TryGetTween(string id, out TransformTween tween)
        {
            tween = TransformTween.None;
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            EnsureCachesBuilt();
            if (tweenLookup != null && tweenLookup.TryGetValue(id, out var value))
            {
                tween = value;
                return true;
            }

            return false;
        }

        private void EnsureCachesBuilt()
        {
            if (!cacheDirty && clipLookup != null && flipbookLookup != null && tweenLookup != null && tweenProviderLookup != null)
            {
                return;
            }

            clipLookup = BuildClipLookup(clipBindings);
            flipbookLookup = BuildFlipbookLookup(flipbookBindings);
            tweenLookup = BuildTweenLookup(tweenBindings);
            tweenProviderLookup = BuildTweenProviderLookup(tweenBindings);
            cacheDirty = false;
        }

        private static Dictionary<string, AnimationClip> BuildClipLookup(AnimationClipBinding[] bindings)
        {
            var dictionary = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);
            if (bindings == null)
            {
                return dictionary;
            }

            for (int i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                if (string.IsNullOrWhiteSpace(binding.Id) || binding.Clip == null)
                {
                    continue;
                }

                dictionary[binding.Id] = binding.Clip;
            }

            return dictionary;
        }

        private static Dictionary<string, FlipbookBinding> BuildFlipbookLookup(FlipbookBinding[] bindings)
        {
            var dictionary = new Dictionary<string, FlipbookBinding>(StringComparer.OrdinalIgnoreCase);
            if (bindings == null)
            {
                return dictionary;
            }

            for (int i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                if (string.IsNullOrWhiteSpace(binding.Id) || binding.Frames == null || binding.Frames.Length == 0)
                {
                    continue;
                }

                dictionary[binding.Id] = binding;
            }

            return dictionary;
        }

        public bool TryGetTweenProvider(string id, out TransformTweenProvider provider)
        {
            provider = null;
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            EnsureCachesBuilt();
            if (tweenProviderLookup != null && tweenProviderLookup.TryGetValue(id, out var value))
            {
                provider = value;
                return provider != null;
            }

            return false;
        }

        private static Dictionary<string, TransformTween> BuildTweenLookup(TransformTweenBinding[] bindings)
        {
            var dictionary = new Dictionary<string, TransformTween>(StringComparer.OrdinalIgnoreCase);
            if (bindings == null)
            {
                return dictionary;
            }

            for (int i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                if (string.IsNullOrWhiteSpace(binding.Id))
                {
                    continue;
                }

                if (binding.Tween.IsValid)
                {
                    dictionary[binding.Id] = binding.Tween;
                }
            }

            return dictionary;
        }

        private static Dictionary<string, TransformTweenProvider> BuildTweenProviderLookup(TransformTweenBinding[] bindings)
        {
            var dictionary = new Dictionary<string, TransformTweenProvider>(StringComparer.OrdinalIgnoreCase);
            if (bindings == null)
            {
                return dictionary;
            }

            for (int i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                if (string.IsNullOrWhiteSpace(binding.Id) || binding.TweenProvider == null)
                {
                    continue;
                }

                dictionary[binding.Id] = binding.TweenProvider;
            }

            return dictionary;
        }
    }

    [Serializable]
    public struct FlipbookBinding
    {
        public string Id;
        public Sprite[] Frames;
        public float FrameRate;
        public bool Loop;
    }

    [Serializable]
    public struct TransformTweenBinding
    {
        public string Id;
        public TransformTween Tween;
        public TransformTweenProvider TweenProvider;
    }
}
