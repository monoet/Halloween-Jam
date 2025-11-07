using System;
using System.Collections.Generic;
using BattleV2.AnimationSystem.Execution.Runtime.Executors;

namespace BattleV2.AnimationSystem.Execution.Runtime.Recipes
{
    public static class PilotActionRecipes
    {
        public const string BasicAttackLightId = "BasicAttack_KS_Light";
        public const string BasicAttackSuccessId = "BasicAttack_KS_Success";
        public const string BasicAttackMediocreId = "BasicAttack_Mediocre";
        public const string UseItemId = "UseItem";
        public const string TurnIntroId = "turn_intro";
        public const string RunUpId = "run_up";
        public const string IdleId = "idle";

        private const string KsWindowId = "ks-light-window";
        private const string KsWindowTag = "KS_Light";

        public static IEnumerable<ActionRecipe> Build(ActionRecipeBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            yield return BuildBasicAttackLight(builder);
            yield return BuildBasicAttackSuccess(builder);
            yield return BuildBasicAttackMediocre(builder);
            yield return BuildUseItem(builder);
            yield return BuildTurnIntro(builder);
            yield return BuildRunUp(builder);
            yield return BuildIdle(builder);
        }

        private static ActionRecipe BuildBasicAttackLight(ActionRecipeBuilder builder)
        {
            var definition = new ActionRecipeBuilder.ActionRecipeDefinition(BasicAttackLightId);

            var introGroup = new ActionRecipeBuilder.GroupDefinition($"{BasicAttackLightId}/Intro")
                .AddStep(AnimatorStep("KS_Light_Windup", ("loop", "false")))
                .AddStep(TweenStep("StepForward", ("duration", "0.25")))
                .AddStep(WaitStep("0.15"))
                .AddStep(SystemStep("window.open",
                    ("id", KsWindowId),
                    ("tag", KsWindowTag),
                    ("start", "0.25"),
                    ("end", "0.45"),
                    ("index", "0"),
                    ("count", "1")))
                .AddStep(SystemStep("gate.on",
                    ("id", KsWindowId),
                    ("success", BasicAttackSuccessId),
                    ("fail", BasicAttackMediocreId),
                    ("timeout", BasicAttackMediocreId),
                    ("successOn", "Perfect,Good"),
                    ("abortOnTimeout", "false"),
                    ("abortOnFail", "false"),
                    ("abortOnSuccess", "false")));

            definition.AddGroup(introGroup);
            definition.AddGroup(CreateSuccessGroupDefinition());
            definition.AddGroup(CreateMediocreGroupDefinition());

            return builder.Build(definition);
        }

        private static ActionRecipe BuildBasicAttackSuccess(ActionRecipeBuilder builder)
        {
            var definition = new ActionRecipeBuilder.ActionRecipeDefinition(BasicAttackSuccessId)
                .AddGroup(new ActionRecipeBuilder.GroupDefinition($"{BasicAttackSuccessId}/Main")
                    .AddSteps(CloneSteps(CreateSuccessSteps(includeWindowClose: false))));

            return builder.Build(definition);
        }

        private static ActionRecipe BuildBasicAttackMediocre(ActionRecipeBuilder builder)
        {
            var definition = new ActionRecipeBuilder.ActionRecipeDefinition(BasicAttackMediocreId)
                .AddGroup(new ActionRecipeBuilder.GroupDefinition($"{BasicAttackMediocreId}/Main")
                    .AddSteps(CloneSteps(CreateMediocreSteps(includeWindowClose: false))));

            return builder.Build(definition);
        }

        private static ActionRecipe BuildUseItem(ActionRecipeBuilder builder)
        {
            var definition = new ActionRecipeBuilder.ActionRecipeDefinition(UseItemId);

            var mainGroup = new ActionRecipeBuilder.GroupDefinition($"{UseItemId}/Main")
                .AddStep(AnimatorStep("Item_Ready", ("loop", "false")))
                .AddStep(WaitStep("0.15"))
                .AddStep(AnimatorStep("Item_Throw", ("loop", "false")))
                .AddStep(SfxStep("sfx_item_throw", ("volume", "0.9")))
                .AddStep(VfxStep("vfx_item_trail", ("socket", "Hand"), ("lifetime", "0.6")));

            definition.AddGroup(mainGroup);
            return builder.Build(definition);
        }

        private static ActionRecipe BuildTurnIntro(ActionRecipeBuilder builder)
        {
            var definition = new ActionRecipeBuilder.ActionRecipeDefinition(TurnIntroId);
            var mainGroup = new ActionRecipeBuilder.GroupDefinition($"{TurnIntroId}/Main")
                .AddStep(AnimatorStep("Ciro_TurnIntro", ("loop", "false")));
            definition.AddGroup(mainGroup);
            return builder.Build(definition);
        }

        private static ActionRecipe BuildRunUp(ActionRecipeBuilder builder)
        {
            var definition = new ActionRecipeBuilder.ActionRecipeDefinition(RunUpId);
            var mainGroup = new ActionRecipeBuilder.GroupDefinition($"{RunUpId}/Main")
                .AddStep(AnimatorStep("Ciro_RunUp", ("loop", "false")));
            definition.AddGroup(mainGroup);
            return builder.Build(definition);
        }

        private static ActionRecipe BuildIdle(ActionRecipeBuilder builder)
        {
            var definition = new ActionRecipeBuilder.ActionRecipeDefinition(IdleId);
            var mainGroup = new ActionRecipeBuilder.GroupDefinition($"{IdleId}/Main")
                .AddStep(AnimatorStep("Ciro_Idle", ("loop", "true")));
            definition.AddGroup(mainGroup);
            return builder.Build(definition);
        }

        private static ActionRecipeBuilder.GroupDefinition CreateSuccessGroupDefinition()
        {
            var group = new ActionRecipeBuilder.GroupDefinition(BasicAttackSuccessId);
            group.AddSteps(CloneSteps(CreateSuccessSteps(includeWindowClose: true)));
            return group;
        }

        private static ActionRecipeBuilder.GroupDefinition CreateMediocreGroupDefinition()
        {
            var group = new ActionRecipeBuilder.GroupDefinition(BasicAttackMediocreId);
            group.AddSteps(CloneSteps(CreateMediocreSteps(includeWindowClose: true)));
            return group;
        }

        private static IEnumerable<ActionRecipeBuilder.StepDefinition> CreateSuccessSteps(bool includeWindowClose)
        {
            if (includeWindowClose)
            {
                yield return SystemStep("window.close",
                    ("id", KsWindowId),
                    ("tag", KsWindowTag));
            }

            yield return AnimatorStep("KS_Light_Success", ("loop", "false"));
            yield return SfxStep("sfx_attack_crit", ("volume", "1.0"));
            yield return VfxStep("vfx_attack_slash", ("socket", "Weapon"), ("intensity", "1.0"));
            yield return TweenStep("LungeForward", ("duration", "0.2"));
            yield return SystemStep("damage.apply", ("formula", "BasicAttack_Success"));
        }

        private static IEnumerable<ActionRecipeBuilder.StepDefinition> CreateMediocreSteps(bool includeWindowClose)
        {
            if (includeWindowClose)
            {
                yield return SystemStep("window.close",
                    ("id", KsWindowId),
                    ("tag", KsWindowTag));
            }

            yield return AnimatorStep("KS_Light_Fail", ("loop", "false"));
            yield return SfxStep("sfx_attack_whiff", ("volume", "0.75"));
            yield return TweenStep("RecoilBack", ("duration", "0.25"));
            yield return SystemStep("damage.apply", ("formula", "BasicAttack_Mediocre"));
            yield return SystemStep("fallback",
                ("timelineId", "BasicAttack_Recover"),
                ("reason", "KsBranchFail"));
        }

        private static IEnumerable<ActionRecipeBuilder.StepDefinition> CloneSteps(IEnumerable<ActionRecipeBuilder.StepDefinition> source)
        {
            foreach (var step in source)
            {
                var clone = new ActionRecipeBuilder.StepDefinition(step.ExecutorId, step.BindingId)
                {
                    Id = step.Id,
                    ConflictPolicy = step.ConflictPolicy,
                    HasExplicitConflictPolicy = step.HasExplicitConflictPolicy,
                    DelaySeconds = step.DelaySeconds
                };
                clone.WithParameters(step.Parameters);
                yield return clone;
            }
        }

        private static ActionRecipeBuilder.StepDefinition AnimatorStep(string bindingId, params (string Key, string Value)[] parameters)
        {
            return new ActionRecipeBuilder.StepDefinition(AnimatorClipExecutor.ExecutorId, bindingId).WithParameters(parameters);
        }

        private static ActionRecipeBuilder.StepDefinition TweenStep(string bindingId, params (string Key, string Value)[] parameters)
        {
            return new ActionRecipeBuilder.StepDefinition(TweenExecutor.ExecutorId, bindingId).WithParameters(parameters);
        }

        private static ActionRecipeBuilder.StepDefinition WaitStep(string seconds)
        {
            return new ActionRecipeBuilder.StepDefinition(WaitExecutor.ExecutorId)
                .WithParameter("seconds", seconds);
        }

        private static ActionRecipeBuilder.StepDefinition SfxStep(string bindingId, params (string Key, string Value)[] parameters)
        {
            return new ActionRecipeBuilder.StepDefinition(SfxExecutor.ExecutorId, bindingId).WithParameters(parameters);
        }

        private static ActionRecipeBuilder.StepDefinition VfxStep(string bindingId, params (string Key, string Value)[] parameters)
        {
            return new ActionRecipeBuilder.StepDefinition(VfxExecutor.ExecutorId, bindingId).WithParameters(parameters);
        }

        private static ActionRecipeBuilder.StepDefinition SystemStep(string executorId, params (string Key, string Value)[] parameters)
        {
            return new ActionRecipeBuilder.StepDefinition(executorId).WithParameters(parameters);
        }

        private static ActionRecipeBuilder.GroupDefinition AddSteps(this ActionRecipeBuilder.GroupDefinition group, IEnumerable<ActionRecipeBuilder.StepDefinition> steps)
        {
            if (group == null || steps == null)
            {
                return group;
            }

            foreach (var step in steps)
            {
                group.AddStep(step);
            }

            return group;
        }
    }
}
