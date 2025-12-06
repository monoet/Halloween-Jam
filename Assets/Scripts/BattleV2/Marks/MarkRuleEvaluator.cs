using BattleV2.Core;
using BattleV2.Execution;

namespace BattleV2.Marks
{
    /// <summary>
    /// Evaluates mark rules against judgments. Keeps randomness deterministic via TargetJudgment.RngSeed.
    /// </summary>
    public sealed class MarkRuleEvaluator
    {
        private readonly MarkService markService;

        public MarkRuleEvaluator(MarkService markService)
        {
            this.markService = markService;
        }

        public bool TryApplyMark(MarkRule rule, ActionJudgment action, TargetJudgment target, CombatantState combatant)
        {
            if (rule == null || rule.mark == null || markService == null)
            {
                return false;
            }

            if (!PassesGates(rule, action))
            {
                return false;
            }

            if (!Roll(rule, target))
            {
                return false;
            }

            return markService.ApplyMark(combatant, rule.mark);
        }

        public bool TryDetonateMark(MarkRule rule, ActionJudgment action, TargetJudgment target, CombatantState combatant)
        {
            if (rule == null || rule.mark == null || markService == null)
            {
                return false;
            }

            var markId = ResolveKey(rule.mark);
            if (!markService.HasMark(combatant, markId))
            {
                return false;
            }

            if (!PassesGates(rule, action))
            {
                return false;
            }

            if (!Roll(rule, target))
            {
                return false;
            }

            if (rule.consumeOnDetonate)
            {
                markService.DetonateMark(combatant, markId, combatant, null);
            }

            // Detonation effect resolution is handled elsewhere; evaluator only clears/keeps state.
            return true;
        }

        private static bool PassesGates(MarkRule rule, ActionJudgment action)
        {
            if (!action.HasValue)
            {
                return false;
            }

            if (rule.requiresCp && action.CpSpent <= 0)
            {
                return false;
            }

            if (rule.requiresTimedSuccess)
            {
                if (action.TimedGrade == TimedGrade.None || action.TimedGrade < rule.minTimedGrade)
                {
                    return false;
                }
            }

            var resources = action.ResourcesPostCost.HasValue ? action.ResourcesPostCost : action.ResourcesPreCost;

            if (rule.cpExact >= 0)
            {
                if (!resources.HasValue || resources.CpCurrent != rule.cpExact)
                {
                    return false;
                }
            }

            if (rule.cpMin > 0)
            {
                if (!resources.HasValue || resources.CpCurrent < rule.cpMin)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Roll(MarkRule rule, TargetJudgment target)
        {
            float chance = rule.chance <= 0f ? 0f : (rule.chance > 1f ? 1f : rule.chance);
            if (chance >= 1f)
            {
                return true;
            }

            var rng = new System.Random(target.RngSeed);
            return rng.NextDouble() <= chance;
        }

        private static string ResolveKey(MarkDefinition definition)
        {
            if (definition == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(definition.id))
            {
                return definition.id;
            }

            return definition.name;
        }
    }
}
