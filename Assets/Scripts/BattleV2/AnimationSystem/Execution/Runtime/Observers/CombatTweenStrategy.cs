using DG.Tweening;
using UnityEngine;

namespace BattleV2.AnimationSystem.Execution.Runtime.Observers
{
    [CreateAssetMenu(fileName = "CombatTweenStrategy", menuName = "Battle/Tween Strategy")]
    public sealed class CombatTweenStrategy : ScriptableObject
    {
        public bool enabled = true;
        [Tooltip("Recipe id (run_up, basic_attack, run_back, etc).")]
        public string recipeId = "run_up";

        [Tooltip("Treat target as localPosition when true, world position otherwise.")]
        public bool useLocalSpace = true;

        [Tooltip("Absolute tween target (interpreted as local/world depending on useLocalSpace).")]
        public Vector3 target = Vector3.zero;

        [Min(0f)]
        public float duration = 0.25f;

        public Ease ease = Ease.OutSine;

        [Tooltip("If true, RecipeTweenObserver will return to the captured initialLocalPos instead of target.")]
        public bool returnToInitialPosition = false;
    }
}
