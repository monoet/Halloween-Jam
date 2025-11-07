using System.Collections.Generic;

namespace BattleV2.AnimationSystem.Runtime
{
    /// <summary>
    /// Allows scene or prefab components to contribute animation actor bindings at installer startup.
    /// </summary>
    public interface IAnimationBindingProvider
    {
        IEnumerable<AnimationActorBinding> GetBindings();
    }
}
