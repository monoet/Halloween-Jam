using System;

namespace BattleV2.AnimationSystem.Execution.Runtime
{
    public enum StepExecutionOutcome
    {
        Completed,
        Branch,
        Skipped,
        Cancelled,
        Faulted
    }

    public readonly struct StepExecutionReport
    {
        public StepExecutionReport(ActionStep step, StepExecutionOutcome outcome, TimeSpan duration)
        {
            Step = step;
            Outcome = outcome;
            Duration = duration;
        }

        public ActionStep Step { get; }
        public StepExecutionOutcome Outcome { get; }
        public TimeSpan Duration { get; }
    }

    public readonly struct StepGroupExecutionReport
    {
        public StepGroupExecutionReport(ActionStepGroup group, TimeSpan duration, bool cancelled)
        {
            Group = group;
            Duration = duration;
            Cancelled = cancelled;
        }

        public ActionStepGroup Group { get; }
        public TimeSpan Duration { get; }
        public bool Cancelled { get; }
    }

    public readonly struct RecipeExecutionReport
    {
        public RecipeExecutionReport(ActionRecipe recipe, TimeSpan duration, bool cancelled)
        {
            Recipe = recipe;
            Duration = duration;
            Cancelled = cancelled;
        }

        public ActionRecipe Recipe { get; }
        public TimeSpan Duration { get; }
        public bool Cancelled { get; }
    }

    public interface IStepSchedulerObserver
    {
        void OnRecipeStarted(ActionRecipe recipe, StepSchedulerContext context);
        void OnRecipeCompleted(RecipeExecutionReport report, StepSchedulerContext context);
        void OnGroupStarted(ActionStepGroup group, StepSchedulerContext context);
        void OnGroupCompleted(StepGroupExecutionReport report, StepSchedulerContext context);
        void OnStepStarted(ActionStep step, StepSchedulerContext context);
        void OnStepCompleted(StepExecutionReport report, StepSchedulerContext context);
    }
}
