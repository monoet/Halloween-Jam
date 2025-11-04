using System.Collections.Generic;
using BattleV2.AnimationSystem.Execution.Runtime.Executors;

namespace BattleV2.AnimationSystem.Execution.Runtime.Recipes
{
    public static class SampleActionRecipes
    {
        public static ActionRecipe BasicAttack => new ActionRecipe(
            "BasicAttack",
            new[]
            {
                new ActionStepGroup(
                    "BasicAttack/Main",
                    new List<ActionStep>
                    {
                        new ActionStep(
                            AnimatorClipExecutor.ExecutorId,
                            "Attack_Windup",
                            Parameters(("loop","false"))),
                        new ActionStep(
                            WaitExecutor.ExecutorId,
                            null,
                            Parameters(("seconds","0.1"))),
                        new ActionStep(
                            AnimatorClipExecutor.ExecutorId,
                            "Attack_Slash",
                            Parameters(("loop","false"))),
                        new ActionStep(
                            SfxExecutor.ExecutorId,
                            "sfx_attack_slash",
                            Parameters()),
                        new ActionStep(
                            VfxExecutor.ExecutorId,
                            "vfx_attack_slash",
                            Parameters(("socket","Weapon"), ("intensity","1.0"))),
                        new ActionStep(
                            TweenExecutor.ExecutorId,
                            "StepForward",
                            Parameters(("duration","0.25")))
                    })
            });

        public static ActionRecipe UseItem => new ActionRecipe(
            "UseItem",
            new[]
            {
                new ActionStepGroup(
                    "UseItem/Main",
                    new List<ActionStep>
                    {
                        new ActionStep(
                            AnimatorClipExecutor.ExecutorId,
                            "Item_Ready",
                            Parameters(("loop","false"))),
                        new ActionStep(
                            WaitExecutor.ExecutorId,
                            null,
                            Parameters(("seconds","0.15"))),
                        new ActionStep(
                            AnimatorClipExecutor.ExecutorId,
                            "Item_Throw",
                            Parameters(("loop","false"))),
                        new ActionStep(
                            SfxExecutor.ExecutorId,
                            "sfx_item_throw",
                            Parameters(("volume","0.9"))),
                        new ActionStep(
                            VfxExecutor.ExecutorId,
                            "vfx_item_trail",
                            Parameters(("socket","Hand"), ("lifetime","0.6")))
                    })
            });

        private static ActionStepParameters Parameters(params (string Key, string Value)[] values)
        {
            if (values == null || values.Length == 0)
            {
                return new ActionStepParameters(null);
            }

            var dict = new Dictionary<string, string>(values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i].Key) && !string.IsNullOrWhiteSpace(values[i].Value))
                {
                    dict[values[i].Key] = values[i].Value;
                }
            }

            return new ActionStepParameters(dict);
        }
    }
}
