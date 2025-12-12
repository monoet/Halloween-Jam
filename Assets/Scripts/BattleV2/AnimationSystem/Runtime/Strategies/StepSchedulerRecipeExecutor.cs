using System;
using System.Threading;
using System.Threading.Tasks;
using BattleV2.Actions;
using BattleV2.AnimationSystem;
using BattleV2.AnimationSystem.Execution;
using BattleV2.AnimationSystem.Execution.Runtime;
using BattleV2.AnimationSystem.Execution.Runtime.Core;
using BattleV2.AnimationSystem.Execution.Runtime.Recipes;
using BattleV2.AnimationSystem.Runtime;
using BattleV2.AnimationSystem.Runtime.Internal;
using UnityEngine;
using BattleV2.Core;
using BattleV2.Orchestration.Runtime;
using BattleV2.Providers;

namespace BattleV2.AnimationSystem.Strategies
{
    /// <summary>
    /// Executes catalog-backed recipes through the StepScheduler when the request is not router-based.
    /// </summary>
    internal sealed class StepSchedulerRecipeExecutor : IRecipeExecutor
    {
        private readonly ActionRecipeCatalog catalog;
        private readonly StepScheduler scheduler;
        private readonly AnimatorRegistry registry;
        private readonly AnimatorWrapperResolver wrapperResolver;
        private readonly AnimationRouterBundle routerBundle;
        private readonly IAnimationEventBus eventBus;
        private readonly ITimedHitService timedHitService;
        private readonly IMainThreadInvoker mainThreadInvoker;

        public StepSchedulerRecipeExecutor(
            ActionRecipeCatalog catalog,
            StepScheduler scheduler,
            AnimatorRegistry registry,
            AnimatorWrapperResolver wrapperResolver,
            AnimationRouterBundle routerBundle,
            IAnimationEventBus eventBus,
            ITimedHitService timedHitService,
            IMainThreadInvoker mainThreadInvoker)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            this.registry = registry ?? AnimatorRegistry.Instance;
            this.wrapperResolver = wrapperResolver;
            this.routerBundle = routerBundle ?? throw new ArgumentNullException(nameof(routerBundle));
            this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            this.timedHitService = timedHitService ?? throw new ArgumentNullException(nameof(timedHitService));
            this.mainThreadInvoker = mainThreadInvoker ?? throw new ArgumentNullException(nameof(mainThreadInvoker));
        }

        public bool CanExecute(string recipeId, StrategyContext context)
        {
            if (string.IsNullOrWhiteSpace(recipeId))
            {
                return false;
            }

            if (recipeId.StartsWith("router:", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return catalog.TryResolveRecipe(recipeId, out _) || scheduler.TryGetRecipe(recipeId, out _);
        }

        public async Task ExecuteAsync(string recipeId, StrategyContext context, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(recipeId))
            {
                return;
            }

            if (!catalog.TryResolveRecipe(recipeId, out var recipe) &&
                !scheduler.TryGetRecipe(recipeId, out recipe))
            {
                context?.LogWarn($"Recipe '{recipeId}' not found in catalog or scheduler.");
                return;
            }

            var animContext = context?.AnimationContext ?? AnimationContext.Default;
            var actor = animContext.PrimaryActor;
            if (actor == null)
            {
                context?.LogWarn($"StepSchedulerRecipeExecutor missing actor for recipe '{recipeId}'.");
                return;
            }

            if (!TryResolveWrapper(actor, out var wrapper))
            {
                context?.LogWarn($"StepSchedulerRecipeExecutor could not resolve wrapper for actor '{actor.name}'.");
                return;
            }

            var participants = animContext.Participants ?? Array.Empty<CombatantState>();
            var selection = new BattleSelection(new BattleActionData { id = recipeId }, animationRecipeId: recipeId);
            var request = new AnimationRequest(actor, selection, participants, 1f, recipeId);

            await mainThreadInvoker.RunAsync(() =>
            {
                routerBundle.RegisterActor(actor);
                return Task.CompletedTask;
            });
            try
            {
                var schedulerContext = new StepSchedulerContext(
                    request,
                    timeline: null,
                    wrapper,
                    wrapper,
                    routerBundle,
                    eventBus,
                    timedHitService,
                    skipResetToFallback: false,
                    gate: new ExternalBarrierGate());

                LogSchedulerExecution(actor, recipe.Id, context);
                await scheduler.ExecuteAsync(recipe, schedulerContext, token);
            }
            finally
            {
                await mainThreadInvoker.RunAsync(() =>
                {
                    routerBundle.UnregisterActor(actor);
                    return Task.CompletedTask;
                });
            }
        }

        private bool TryResolveWrapper(CombatantState actor, out IAnimationWrapper wrapper)
        {
            wrapper = null;
            if (registry != null && registry.TryGetWrapper(actor, out wrapper) && wrapper != null)
            {
                return true;
            }

            var legacyWrapper = wrapperResolver?.Resolve(actor);
            if (legacyWrapper != null)
            {
                wrapper = registry?.ResolveLegacyWrapper(actor, legacyWrapper);
                if (wrapper != null)
                {
                    return true;
                }
            }

            return false;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void LogSchedulerExecution(CombatantState actor, string recipeId, StrategyContext strategyContext)
        {
            string sessionId = "(session-null)";
            if (strategyContext != null)
            {
                var animContext = strategyContext.AnimationContext;
                sessionId = !string.IsNullOrWhiteSpace(animContext.SessionId) ? animContext.SessionId : "(session-null)";
            }

#if false
            Debug.Log($"TTDebug05 [SCHED_EXEC] actor={actor?.name ?? "(null)"} recipe={recipeId ?? "(null)"} session={sessionId}");
#endif
        }
#else
        private static void LogSchedulerExecution(CombatantState actor, string recipeId, StrategyContext strategyContext) { }
#endif
    }
}
