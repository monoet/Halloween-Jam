using System;
using System.Collections.Generic;
using BattleV2.AnimationSystem.Execution;
using BattleV2.Core;

namespace BattleV2.AnimationSystem.Runtime
{
    public sealed class AnimatorWrapperResolver : IDisposable
    {
        private readonly Dictionary<CombatantState, AnimatorWrapper> wrappers = new();
        private readonly Dictionary<CombatantState, AnimatorWrapperBinding> bindings = new();

        public AnimatorWrapperResolver(IEnumerable<AnimationActorBinding> actorBindings)
        {
            if (actorBindings == null)
            {
                return;
            }

            foreach (var binding in actorBindings)
            {
                AddOrUpdateBinding(binding);
            }
        }

        public AnimatorWrapper Resolve(CombatantState actor)
        {
            if (actor == null)
            {
                return null;
            }

            if (wrappers.TryGetValue(actor, out var existing))
            {
                return existing;
            }

            if (!bindings.TryGetValue(actor, out var binding))
            {
                return null;
            }

            var wrapper = new AnimatorWrapper(binding);
            wrappers[actor] = wrapper;
            return wrapper;
        }

        public void Dispose()
        {
            foreach (var kvp in wrappers)
            {
                kvp.Value?.Dispose();
            }

            wrappers.Clear();
            bindings.Clear();
        }

        public void AddOrUpdateBinding(AnimationActorBinding binding)
        {
            if (binding == null || !binding.IsValid)
            {
                return;
            }

            var wrapperBinding = new AnimatorWrapperBinding(binding.Animator, binding.FallbackClip, binding.Sockets);
            bindings[binding.Actor] = wrapperBinding;

            if (wrappers.TryGetValue(binding.Actor, out var existing))
            {
                existing.Dispose();
                wrappers.Remove(binding.Actor);
            }
        }
    }
}
