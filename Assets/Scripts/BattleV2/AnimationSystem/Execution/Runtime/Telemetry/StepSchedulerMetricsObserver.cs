using System;
using System.Collections.Generic;
using BattleV2.Core;

namespace BattleV2.AnimationSystem.Execution.Runtime.Telemetry
{
    public sealed class StepSchedulerMetricsObserver : IStepSchedulerObserver
    {
        private readonly Dictionary<string, RecipeMetrics> recipeMetrics = new Dictionary<string, RecipeMetrics>(StringComparer.OrdinalIgnoreCase);
        private int totalStepsExecuted;
        private int totalStepsSkipped;
        private int totalStepsCancelled;

        public IReadOnlyDictionary<string, RecipeMetrics> RecipeMetrics => recipeMetrics;
        public int TotalStepsExecuted => totalStepsExecuted;
        public int TotalStepsSkipped => totalStepsSkipped;
        public int TotalStepsCancelled => totalStepsCancelled;

        public void OnRecipeStarted(ActionRecipe recipe, StepSchedulerContext context)
        {
            // No-op; we only track completion metrics.
        }

        public void OnRecipeCompleted(RecipeExecutionReport report, StepSchedulerContext context)
        {
            if (report.Recipe == null)
            {
                return;
            }

            var id = report.Recipe.Id ?? "(null)";
            if (!recipeMetrics.TryGetValue(id, out var metrics))
            {
                metrics = new RecipeMetrics(id);
            }

            metrics = metrics.WithExecution(report.Duration, report.Cancelled);
            recipeMetrics[id] = metrics;

            BattleLogger.Log("AnimTelemetry", $"Recipe '{id}' executed in {report.Duration.TotalMilliseconds:F1} ms (cancelled={report.Cancelled}).");
        }

        public void OnGroupStarted(ActionStepGroup group, StepSchedulerContext context)
        {
            // Intentionally left blank.
        }

        public void OnGroupCompleted(StepGroupExecutionReport report, StepSchedulerContext context)
        {
            // No aggregated metrics yet per group.
        }

        public void OnStepCompleted(StepExecutionReport report, StepSchedulerContext context)
        {
            switch (report.Outcome)
            {
                case StepExecutionOutcome.Completed:
                    totalStepsExecuted++;
                    break;
                case StepExecutionOutcome.Skipped:
                    totalStepsSkipped++;
                    break;
                case StepExecutionOutcome.Cancelled:
                    totalStepsCancelled++;
                    break;
            }
        }

        public StepSchedulerMetricsSnapshot CreateSnapshot()
        {
            return new StepSchedulerMetricsSnapshot(totalStepsExecuted, totalStepsSkipped, totalStepsCancelled, recipeMetrics);
        }
    }

    public readonly struct RecipeMetrics
    {
        public RecipeMetrics(string id)
        {
            Id = id ?? "(null)";
            Executions = 0;
            TotalDuration = TimeSpan.Zero;
            CancelledCount = 0;
        }

        private RecipeMetrics(string id, int executions, TimeSpan totalDuration, int cancelledCount)
        {
            Id = id;
            Executions = executions;
            TotalDuration = totalDuration;
            CancelledCount = cancelledCount;
        }

        public string Id { get; }
        public int Executions { get; }
        public TimeSpan TotalDuration { get; }
        public int CancelledCount { get; }
        public TimeSpan AverageDuration => Executions > 0 ? TimeSpan.FromMilliseconds(TotalDuration.TotalMilliseconds / Executions) : TimeSpan.Zero;

        public RecipeMetrics WithExecution(TimeSpan duration, bool cancelled)
        {
            var executions = Executions + 1;
            var totalDuration = TotalDuration + duration;
            var cancelledCount = cancelled ? CancelledCount + 1 : CancelledCount;
            return new RecipeMetrics(Id, executions, totalDuration, cancelledCount);
        }
    }

    public readonly struct StepSchedulerMetricsSnapshot
    {
        public StepSchedulerMetricsSnapshot(
            int executed,
            int skipped,
            int cancelled,
            IReadOnlyDictionary<string, RecipeMetrics> recipeMetrics)
        {
            ExecutedSteps = executed;
            SkippedSteps = skipped;
            CancelledSteps = cancelled;
            RecipeMetrics = recipeMetrics != null
                ? new Dictionary<string, RecipeMetrics>(recipeMetrics)
                : new Dictionary<string, RecipeMetrics>();
        }

        public int ExecutedSteps { get; }
        public int SkippedSteps { get; }
        public int CancelledSteps { get; }
        public IReadOnlyDictionary<string, RecipeMetrics> RecipeMetrics { get; }
    }
}
