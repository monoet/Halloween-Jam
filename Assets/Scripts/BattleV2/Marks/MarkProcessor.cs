using System.Collections.Generic;
using BattleV2.Core;
using BattleV2.Execution;
using BattleV2.Providers;

namespace BattleV2.Marks
{
    /// <summary>
    /// Applies/detonates marks based on rules, using deterministic per-target judgment.
    /// </summary>
    public sealed class MarkProcessor
    {
        private readonly MarkService markService;
        private readonly MarkRuleEvaluator evaluator;

        public MarkProcessor(MarkService markService)
        {
            this.markService = markService;
            evaluator = new MarkRuleEvaluator(markService);
        }

        public void Process(BattleSelection selection, ActionJudgment judgment, IReadOnlyList<CombatantState> targets)
        {
            if (markService == null || selection.Action == null || !judgment.HasValue)
            {
                return;
            }

            var rules = selection.Action.markRules;
            if (rules == null || rules.Count == 0)
            {
                return;
            }

            // Apply first.
            ApplyRules(rules, MarkRuleKind.Apply, selection, judgment, targets);
            // Then detonate.
            ApplyRules(rules, MarkRuleKind.Detonate, selection, judgment, targets);
        }

        private void ApplyRules(
            IList<MarkRule> rules,
            MarkRuleKind kind,
            BattleSelection selection,
            ActionJudgment judgment,
            IReadOnlyList<CombatantState> targets)
        {
            if (rules == null || targets == null)
            {
                return;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null || !target.IsAlive)
                {
                    continue;
                }

                var targetJudgment = TargetJudgment.From(judgment, i, target);

                for (int r = 0; r < rules.Count; r++)
                {
                    var rule = rules[r];
                    if (rule == null || rule.kind != kind)
                    {
                        continue;
                    }

                    if (kind == MarkRuleKind.Apply)
                    {
                        evaluator.TryApplyMark(rule, judgment, targetJudgment, target);
                    }
                    else
                    {
                        evaluator.TryDetonateMark(rule, judgment, targetJudgment, target);
                    }
                }
            }
        }
    }
}
