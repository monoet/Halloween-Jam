using System;
using System.Collections.Generic;
using System.Globalization;

namespace BattleV2.AnimationSystem.Execution.Runtime
{
    /// <summary>
    /// Shared utilities to translate textual step definitions into <see cref="ActionStep"/> instances.
    /// Keeps the legacy inline format available for the catalog builder and timeline adapters.
    /// </summary>
    internal static class ActionStepParser
    {
        public static bool TryParse(string definition, out ActionStep step)
        {
            step = default;
            if (string.IsNullOrWhiteSpace(definition))
            {
                return false;
            }

            string prefix = definition;
            string parameterSection = null;

            int paramStart = definition.IndexOf('(');
            if (paramStart >= 0)
            {
                int paramEnd = definition.LastIndexOf(')');
                if (paramEnd <= paramStart)
                {
                    return false;
                }

                prefix = definition.Substring(0, paramStart);
                parameterSection = definition.Substring(paramStart + 1, paramEnd - paramStart - 1);
            }

            prefix = prefix.Trim();
            if (prefix.Length == 0)
            {
                return false;
            }

            string executorId = prefix;
            string bindingId = null;
            int bindingSeparator = prefix.IndexOf(':');
            if (bindingSeparator >= 0)
            {
                executorId = prefix.Substring(0, bindingSeparator).Trim();
                bindingId = prefix.Substring(bindingSeparator + 1).Trim();
                if (bindingId.Length == 0)
                {
                    bindingId = null;
                }
            }

            if (string.IsNullOrWhiteSpace(executorId))
            {
                return false;
            }

            var parameters = ParseParameters(parameterSection);

            if (bindingId == null && parameters.TryGetValue("binding", out var bindingFromParameter))
            {
                bindingId = bindingFromParameter;
                parameters.Remove("binding");
            }

            string stepId = null;
            if (parameters.TryGetValue("id", out var idValue))
            {
                stepId = idValue;
                parameters.Remove("id");
            }

            StepConflictPolicy conflictPolicy = StepConflictPolicy.WaitForCompletion;
            bool conflictPolicyExplicit = false;
            if (parameters.TryGetValue("conflict", out var conflictValue) &&
                Enum.TryParse(conflictValue, true, out StepConflictPolicy parsedPolicy))
            {
                conflictPolicy = parsedPolicy;
                conflictPolicyExplicit = true;
                parameters.Remove("conflict");
            }

            float delay = 0f;
            if (parameters.TryGetValue("delay", out var delayValue) &&
                float.TryParse(delayValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedDelay))
            {
                delay = Math.Max(0f, parsedDelay);
                parameters.Remove("delay");
            }

            var actionParameters = new ActionStepParameters(parameters);
            step = new ActionStep(executorId, bindingId, actionParameters, conflictPolicy, stepId, delay, conflictPolicyExplicit);
            return true;
        }

        public static IEnumerable<ActionStep> ParseList(string rawDefinitions, char separator = '|')
        {
            if (string.IsNullOrWhiteSpace(rawDefinitions))
            {
                yield break;
            }

            var definitions = rawDefinitions.Split(separator, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < definitions.Length; i++)
            {
                var definition = definitions[i].Trim();
                if (definition.Length == 0)
                {
                    continue;
                }

                if (TryParse(definition, out var step))
                {
                    yield return step;
                }
            }
        }

        private static Dictionary<string, string> ParseParameters(string parameterSection)
        {
            var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(parameterSection))
            {
                return parameters;
            }

            var pairs = parameterSection.Split(',', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < pairs.Length; i++)
            {
                var pair = pairs[i].Trim();
                if (pair.Length == 0)
                {
                    continue;
                }

                int equalsIndex = pair.IndexOf('=');
                if (equalsIndex <= 0 || equalsIndex >= pair.Length - 1)
                {
                    continue;
                }

                var key = pair.Substring(0, equalsIndex).Trim();
                var value = pair.Substring(equalsIndex + 1).Trim();
                if (key.Length == 0 || value.Length == 0)
                {
                    continue;
                }

                parameters[key] = value;
            }

            return parameters;
        }
    }
}
