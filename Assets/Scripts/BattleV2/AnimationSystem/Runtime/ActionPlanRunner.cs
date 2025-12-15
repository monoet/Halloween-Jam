using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BattleV2.AnimationSystem.Execution.Runtime;
using BattleV2.Core;
using BattleV2.Diagnostics;

namespace BattleV2.AnimationSystem.Runtime
{
    /// <summary>
    /// Chunk 4.5 scaffolding: runs a sequence of scheduler recipes as a single action envelope.
    /// Intentionally minimal: caller builds the plan and decides IDs/inline recipes.
    /// </summary>
    public sealed class ActionPlanRunner
    {
        private readonly StepScheduler scheduler;

        public ActionPlanRunner(StepScheduler scheduler)
        {
            this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        }

        public async Task RunAsync(IReadOnlyList<ActionPlanNode> nodes, StepSchedulerContext context, CancellationToken token)
        {
            if (nodes == null || nodes.Count == 0)
            {
                return;
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                var node = nodes[i];
                if (node.IsEmpty)
                {
                    continue;
                }

                if (BattleDebug.MainThreadId >= 0 && BattleDebug.IsEnabled("AP"))
                {
                    BattleDebug.Log("AP", 10, $"start node[{i}] label={node.Label} recipeId={node.RecipeId ?? "(inline)"}");
                }

                var started = DateTime.UtcNow;
                if (node.InlineRecipe != null)
                {
                    await scheduler.ExecuteAsync(node.InlineRecipe, context, token);
                }
                else if (!string.IsNullOrWhiteSpace(node.RecipeId))
                {
                    if (!scheduler.TryGetRecipe(node.RecipeId, out var recipe))
                    {
                        BattleLogger.Warn("ActionPlan", $"Recipe '{node.RecipeId}' not found in scheduler registry.");
                        continue;
                    }

                    await scheduler.ExecuteAsync(recipe, context, token);
                }

                if (BattleDebug.MainThreadId >= 0 && BattleDebug.IsEnabled("AP"))
                {
                    var elapsed = DateTime.UtcNow - started;
                    BattleDebug.Log("AP", 11, $"end node[{i}] ms={(int)elapsed.TotalMilliseconds} label={node.Label}");
                }
            }
        }
    }

    public readonly struct ActionPlanNode
    {
        public ActionPlanNode(string label, string recipeId)
        {
            Label = label;
            RecipeId = recipeId;
            InlineRecipe = null;
        }

        public ActionPlanNode(string label, ActionRecipe inlineRecipe)
        {
            Label = label;
            InlineRecipe = inlineRecipe;
            RecipeId = null;
        }

        public string Label { get; }
        public string RecipeId { get; }
        public ActionRecipe InlineRecipe { get; }

        public bool IsEmpty => string.IsNullOrWhiteSpace(RecipeId) && InlineRecipe == null;
    }
}
