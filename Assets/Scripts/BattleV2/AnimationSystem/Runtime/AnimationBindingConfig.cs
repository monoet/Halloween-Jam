using System;
using System.Collections.Generic;
using BattleV2.Core;
using UnityEngine;

namespace BattleV2.AnimationSystem.Runtime
{
    [Serializable]
    public sealed class AnimationActorBinding
    {
        [Tooltip("Combatant that owns this presentation entry.")]
        public CombatantState Actor;

        [Tooltip("Animator component driven by the AnimatorWrapper.")]
        public Animator Animator;

        [Tooltip("Idle/fallback pose used when no clip is playing.")]
        public AnimationClip FallbackClip;

        [Tooltip("Optional sockets used by routers (VFX spawn points, etc.).")]
        public Transform[] Sockets;

        public AnimationActorBinding() { }

        public AnimationActorBinding(CombatantState actor, Animator animator, AnimationClip fallbackClip, Transform[] sockets = null)
        {
            Actor = actor;
            Animator = animator;
            FallbackClip = fallbackClip;
            Sockets = sockets ?? Array.Empty<Transform>();
        }

        public bool IsValid => Actor != null && Animator != null;
    }

    [Serializable]
    public struct AnimationClipBinding
    {
        public string Id;
        public AnimationClip Clip;
    }

    public sealed class AnimationClipResolver
    {
        private readonly Dictionary<string, AnimationClip> lookup;

        public AnimationClipResolver(IEnumerable<AnimationClipBinding> bindings)
        {
            lookup = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);
            if (bindings == null)
            {
                return;
            }

            RegisterBindings(bindings);
        }

        public void RegisterBindings(IEnumerable<AnimationClipBinding> bindings)
        {
            if (bindings == null)
            {
                return;
            }

            foreach (var binding in bindings)
            {
                if (string.IsNullOrWhiteSpace(binding.Id) || binding.Clip == null)
                {
                    continue;
                }

                if (lookup.TryGetValue(binding.Id, out var existingClip))
                {
                    if (existingClip == binding.Clip)
                    {
                        continue;
                    }

                    Debug.LogWarning($"[AnimationClipResolver] Overriding clip binding for id '{binding.Id}'.", binding.Clip);
                }

                lookup[binding.Id] = binding.Clip;
            }
        }

        public bool TryGetClip(string clipId, out AnimationClip clip)
        {
            if (string.IsNullOrWhiteSpace(clipId))
            {
                clip = null;
                return false;
            }

            return lookup.TryGetValue(clipId, out clip);
        }
    }
}
