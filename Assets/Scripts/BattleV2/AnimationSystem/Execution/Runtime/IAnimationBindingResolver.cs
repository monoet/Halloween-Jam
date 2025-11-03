using BattleV2.Orchestration.Runtime;
using UnityEngine;

namespace BattleV2.AnimationSystem.Execution.Runtime
{
    public interface IAnimationBindingResolver
    {
        bool TryGetClip(string id, out AnimationClip clip);
        bool TryGetFlipbook(string id, out FlipbookBinding binding);
        bool TryGetTween(string id, out TransformTween tween);
    }
}
