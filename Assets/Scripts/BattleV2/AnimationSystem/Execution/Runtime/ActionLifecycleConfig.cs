using System;

namespace BattleV2.AnimationSystem.Execution.Runtime
{
    /// <summary>
    /// Declarative configuration telling the scheduler which recipes should run before/after an action.
    /// </summary>
    public sealed class ActionLifecycleConfig
    {
        public static ActionLifecycleConfig Default { get; } = new ActionLifecycleConfig();

        public string RunUpRecipeId { get; set; } = "run_up";
        public string RunBackRecipeId { get; set; } = "run_back";

        public bool HasRunUp => !string.IsNullOrWhiteSpace(RunUpRecipeId);
        public bool HasRunBack => !string.IsNullOrWhiteSpace(RunBackRecipeId);
    }

    public enum ActionLifecyclePhase
    {
        PreAction,
        Action,
        PostAction
    }

    public enum ActionLifecycleEventType
    {
        Begin,
        End
    }

    public readonly struct ActionLifecycleEventArgs
    {
        public ActionLifecycleEventArgs(
            string eventId,
            ActionLifecyclePhase phase,
            ActionLifecycleEventType eventType,
            ActionRecipe recipe,
            StepSchedulerContext context)
        {
            EventId = eventId ?? string.Empty;
            Phase = phase;
            EventType = eventType;
            Recipe = recipe;
            Context = context;
        }

        public string EventId { get; }
        public ActionLifecyclePhase Phase { get; }
        public ActionLifecycleEventType EventType { get; }
        public ActionRecipe Recipe { get; }
        public StepSchedulerContext Context { get; }
        public CombatantState Actor => Context.Actor;
    }

    public static class ActionLifecycleEvents
    {
        public const string PreActionBegin = "on_pre_action_begin";
        public const string PreActionEnd = "on_pre_action_end";
        public const string ActionBegin = "on_action_begin";
        public const string ActionEnd = "on_action_end";
        public const string PostActionBegin = "on_post_action_begin";
        public const string PostActionEnd = "on_post_action_end";

        public static string GetEventId(ActionLifecyclePhase phase, ActionLifecycleEventType type)
        {
            return phase switch
            {
                ActionLifecyclePhase.PreAction => type == ActionLifecycleEventType.Begin ? PreActionBegin : PreActionEnd,
                ActionLifecyclePhase.Action => type == ActionLifecycleEventType.Begin ? ActionBegin : ActionEnd,
                ActionLifecyclePhase.PostAction => type == ActionLifecycleEventType.Begin ? PostActionBegin : PostActionEnd,
                _ => string.Empty
            };
        }
    }
}
