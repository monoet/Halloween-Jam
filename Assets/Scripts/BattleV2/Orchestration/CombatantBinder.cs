using BattleV2.Anim;
using UnityEngine;

namespace BattleV2.Orchestration
{
    /// <summary>
    /// Centralized helper that binds a combatant to its runtime data and presentation hooks.
    /// </summary>
    public static class CombatantBinder
    {
        public readonly struct BindingResult
        {
            public BindingResult(
                CombatantState combatant,
                CharacterRuntime runtime,
                BattleAnimationController animationController)
            {
                Combatant = combatant;
                Runtime = runtime;
                AnimationController = animationController;
            }

            public CombatantState Combatant { get; }
            public CharacterRuntime Runtime { get; }
            public BattleAnimationController AnimationController { get; }
        }

        /// <summary>
        /// Binds the supplied combatant to its CharacterRuntime, ensuring archetype data is applied.
        /// Returns a binding result that presentation layers can use.
        /// </summary>
        public static bool TryBind(
            CombatantState combatant,
            bool preserveVitals,
            out BindingResult result)
        {
            result = default;

            if (combatant == null)
            {
                return false;
            }

            var runtime = combatant.CharacterRuntime ?? combatant.GetComponent<CharacterRuntime>();
            if (runtime == null)
            {
                Debug.LogWarning($"[CombatantBinder] Combatant '{combatant.name}' is missing CharacterRuntime. Binding skipped.", combatant);
                return false;
            }

            if (runtime.Archetype == null)
            {
                Debug.LogWarning($"[CombatantBinder] Runtime '{runtime.name}' for combatant '{combatant.name}' has no Archetype assigned.", runtime);
            }

            combatant.SetCharacterRuntime(runtime, initialize: true, preserveVitals: preserveVitals);

            var animationController = combatant.GetComponentInChildren<BattleAnimationController>();
            result = new BindingResult(combatant, runtime, animationController);
            return true;
        }
    }
}
