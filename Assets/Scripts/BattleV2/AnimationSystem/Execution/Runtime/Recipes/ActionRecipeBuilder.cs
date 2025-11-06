using System;
using System.Collections.Generic;
using BattleV2.AnimationSystem.Timelines;

namespace BattleV2.AnimationSystem.Execution.Runtime.Recipes
{
    /// <summary>
    /// Provides helpers to translate authored data (blueprints, timelines) into <see cref="ActionRecipe"/> instances.
    /// </summary>
    public sealed class ActionRecipeBuilder
    {
        public ActionRecipe Build(ActionRecipeDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var groups = new List<ActionStepGroup>(definition.Groups.Count);
            for (int i = 0; i < definition.Groups.Count; i++)
            {
                var groupDefinition = definition.Groups[i];
                if (groupDefinition == null || groupDefinition.Steps.Count == 0)
                {
                    continue;
                }

                var steps = new List<ActionStep>(groupDefinition.Steps.Count);
                for (int j = 0; j < groupDefinition.Steps.Count; j++)
                {
                    var stepDefinition = groupDefinition.Steps[j];
                    if (stepDefinition == null)
                    {
                        continue;
                    }

                    steps.Add(CreateStep(stepDefinition));
                }

                if (steps.Count == 0)
                {
                    continue;
                }

                groups.Add(new ActionStepGroup(
                    groupDefinition.Id,
                    steps,
                    groupDefinition.ExecutionMode,
                    groupDefinition.JoinPolicy,
                    Math.Max(0f, groupDefinition.TimeoutSeconds)));
            }

            if (groups.Count == 0)
            {
                return new ActionRecipe(definition.Id, Array.Empty<ActionStepGroup>());
            }

            return new ActionRecipe(definition.Id, groups);
        }

        /// <summary>
        /// Minimal adapter that converts timeline events containing inline step definitions
        /// (e.g. "animatorClip:Binding(loop=false)|sfx:Hit") into scheduler recipes.
        /// </summary>
        public ActionRecipe BuildFromTimeline(ActionTimeline timeline)
        {
            if (timeline == null)
            {
                throw new ArgumentNullException(nameof(timeline));
            }

            var groups = new List<ActionStepGroup>();
            var tracks = timeline.Tracks;
            if (tracks != null)
            {
                for (int i = 0; i < tracks.Count; i++)
                {
                    var track = tracks[i];
                    if (track.Phases == null || track.Phases.Count == 0)
                    {
                        continue;
                    }

                    var steps = new List<ActionStep>();
                    for (int j = 0; j < track.Phases.Count; j++)
                    {
                        var phase = track.Phases[j];
                        AppendStepsFromEvent(phase.OnEnterEvent, steps);
                        AppendStepsFromEvent(phase.OnExitEvent, steps);
                    }

                    if (steps.Count == 0)
                    {
                        continue;
                    }

                    string groupId = $"{timeline.ActionId}/{track.Type}_{i}";
                    groups.Add(new ActionStepGroup(groupId, steps, StepGroupExecutionMode.Sequential));
                }
            }

            if (groups.Count == 0)
            {
                return new ActionRecipe(timeline.ActionId ?? "(timeline)", Array.Empty<ActionStepGroup>());
            }

            return new ActionRecipe(timeline.ActionId ?? "(timeline)", groups);
        }

        private static void AppendStepsFromEvent(string eventPayload, List<ActionStep> steps)
        {
            if (string.IsNullOrWhiteSpace(eventPayload))
            {
                return;
            }

            foreach (var step in ActionStepParser.ParseList(eventPayload))
            {
                steps.Add(step);
            }
        }

        private static ActionStep CreateStep(StepDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (string.IsNullOrWhiteSpace(definition.ExecutorId))
            {
                throw new ArgumentException("StepDefinition requires a valid executor id.", nameof(definition));
            }

            ActionStepParameters parameters = definition.Parameters.Count == 0
                ? new ActionStepParameters(null)
                : new ActionStepParameters(new Dictionary<string, string>(definition.Parameters, StringComparer.OrdinalIgnoreCase));

            return new ActionStep(
                definition.ExecutorId,
                definition.BindingId,
                parameters,
                definition.ConflictPolicy,
                definition.Id,
                Math.Max(0f, definition.DelaySeconds),
                definition.HasExplicitConflictPolicy);
        }

        #region Blueprint definitions

        public sealed class ActionRecipeDefinition
        {
            public ActionRecipeDefinition(string id)
            {
                Id = string.IsNullOrWhiteSpace(id) ? "(unnamed)" : id;
            }

            public string Id { get; }
            public List<GroupDefinition> Groups { get; } = new List<GroupDefinition>();

            public ActionRecipeDefinition AddGroup(GroupDefinition group)
            {
                if (group != null)
                {
                    Groups.Add(group);
                }

                return this;
            }
        }

        public sealed class GroupDefinition
        {
            public GroupDefinition(string id = null)
            {
                Id = id;
            }

            public string Id { get; set; }
            public StepGroupExecutionMode ExecutionMode { get; set; } = StepGroupExecutionMode.Sequential;
            public StepGroupJoinPolicy JoinPolicy { get; set; } = StepGroupJoinPolicy.All;
            public float TimeoutSeconds { get; set; }
            public List<StepDefinition> Steps { get; } = new List<StepDefinition>();

            public GroupDefinition AddStep(StepDefinition definition)
            {
                if (definition != null)
                {
                    Steps.Add(definition);
                }

                return this;
            }
        }

        public sealed class StepDefinition
        {
            public StepDefinition(string executorId, string bindingId = null)
            {
                ExecutorId = executorId ?? throw new ArgumentNullException(nameof(executorId));
                BindingId = bindingId;
            }

            public string Id { get; set; }
            public string ExecutorId { get; }
            public string BindingId { get; }
            public StepConflictPolicy ConflictPolicy { get; set; } = StepConflictPolicy.WaitForCompletion;
            public bool HasExplicitConflictPolicy { get; set; }
            public float DelaySeconds { get; set; }
            public Dictionary<string, string> Parameters { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            public StepDefinition WithParameter(string key, string value)
            {
                if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                {
                    Parameters[key] = value;
                }

                return this;
            }

            public StepDefinition WithParameters(params (string Key, string Value)[] pairs)
            {
                if (pairs == null)
                {
                    return this;
                }

                for (int i = 0; i < pairs.Length; i++)
                {
                    WithParameter(pairs[i].Key, pairs[i].Value);
                }

                return this;
            }

            public StepDefinition WithParameters(IEnumerable<KeyValuePair<string, string>> pairs)
            {
                if (pairs == null)
                {
                    return this;
                }

                foreach (var pair in pairs)
                {
                    WithParameter(pair.Key, pair.Value);
                }

                return this;
            }

            public StepDefinition WithConflictPolicy(StepConflictPolicy policy, bool explicitPolicy = true)
            {
                ConflictPolicy = policy;
                HasExplicitConflictPolicy = explicitPolicy;
                return this;
            }

            public StepDefinition WithDelay(float seconds)
            {
                DelaySeconds = Math.Max(0f, seconds);
                return this;
            }
        }

        #endregion
    }
}
