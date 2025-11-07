using System.Collections.Generic;
using UnityEngine;
using BattleV2.AnimationSystem.Runtime;
using BattleV2.Orchestration.Runtime;

namespace BattleV2.AnimationSystem.Runtime.Bindings
{
    [DisallowMultipleComponent]
    public sealed class CharacterAnimationSetProvider : MonoBehaviour, IAnimationBindingProvider
    {
        [SerializeField] private CombatantState actor;
        [SerializeField] private Animator animator;
        [SerializeField] private AnimationClip fallbackClip;
        [SerializeField] private Transform[] sockets;
        [SerializeField] private CharacterAnimationSet animationSet;

        public CharacterAnimationSet AnimationSet => animationSet;

        public IEnumerable<AnimationActorBinding> GetBindings()
        {
            var resolvedActor = ResolveActor();
            var resolvedAnimator = ResolveAnimator();

            if (resolvedActor == null || resolvedAnimator == null)
            {
                yield break;
            }

            var resolvedFallback = fallbackClip;
            if (resolvedFallback == null && animationSet != null)
            {
                var clips = animationSet.ClipBindings;
                if (clips != null && clips.Count > 0)
                {
                    resolvedFallback = clips[0].Clip;
                }
            }

            yield return new AnimationActorBinding(
                resolvedActor,
                resolvedAnimator,
                resolvedFallback,
                sockets);
        }

        private CombatantState ResolveActor()
        {
            if (actor != null)
            {
                return actor;
            }

            var parents = GetComponentsInParent<CombatantState>(true);
            return parents != null && parents.Length > 0 ? parents[0] : null;
        }

        private Animator ResolveAnimator()
        {
            if (animator != null)
            {
                return animator;
            }

            var resolvedActor = ResolveActor();
            if (resolvedActor != null)
            {
                var animators = resolvedActor.GetComponentsInChildren<Animator>(true);
                if (animators != null && animators.Length > 0)
                {
                    return animators[0];
                }
            }

            var fallback = GetComponentsInChildren<Animator>(true);
            return fallback != null && fallback.Length > 0 ? fallback[0] : null;
        }

#if UNITY_EDITOR
        private void Reset()
        {
            actor = GetComponentInParent<CombatantState>();
            animator = GetComponentInChildren<Animator>();
        }
#endif
    }
}
