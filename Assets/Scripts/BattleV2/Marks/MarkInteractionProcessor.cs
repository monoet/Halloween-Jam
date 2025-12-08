using System.Collections.Generic;
using BattleV2.Execution;
using BattleV2.Providers;
using BattleV2.Targeting;

namespace BattleV2.Marks
{
    /// <summary>
    /// Applier de Marks basado en reglas puras. No mantiene estado; usa MarkService + MarkRulesEngine.
    /// </summary>
    public sealed class MarkInteractionProcessor
    {
        private readonly MarkService markService;
        private readonly float aoePerCpBonus;

        public MarkInteractionProcessor(MarkService markService, float aoePerCpBonus = 0.1f)
        {
            this.markService = markService;
            this.aoePerCpBonus = aoePerCpBonus;
        }

        public void Process(
            CombatantState attacker,
            BattleSelection selection,
            ActionJudgment judgment,
            IReadOnlyList<CombatantState> targets,
            int executionId,
            int attackerTurnCounter = 0)
        {
            if (markService == null || selection.Action == null || targets == null)
            {
                return;
            }

            var rules = selection.Action.markRules;
            if (rules == null || rules.Count == 0)
            {
                return;
            }

            int attackerId = attacker != null ? attacker.StableId : 0;
            var timedGrade = judgment.HasValue ? judgment.TimedGrade : TimedGrade.None;
            bool isAoE = selection.Action.targetShape == TargetShape.All;

            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null || !target.IsAlive || !target.IsEnemy) // v1: solo enemigos (usa side, no IsPlayer)
                {
                    continue;
                }

                var targetJudgment = TargetJudgment.From(judgment, i, target);

                for (int r = 0; r < rules.Count; r++)
                {
                    var rule = rules[r];
                    if (rule == null || rule.mark == null)
                    {
                        continue;
                    }

                    var incoming = MarkElementView.FromDefinition(rule.mark);
                    if (incoming.IsNone || !incoming.CanDetonateMarks)
                    {
                        continue;
                    }

                    bool qualifies = isAoE
                        ? MarkRulesEngine.QualifiesForMarksAoETarget(judgment.CpSpent, incoming, aoePerCpBonus, targetJudgment.RngSeed, out _, out _)
                        : MarkRulesEngine.QualifiesForMarksSingle(judgment.CpSpent, timedGrade, incoming);

                    if (!qualifies)
                    {
                        continue;
                    }

                    var interaction = MarkRulesEngine.ResolveInteraction(target.ActiveMark, incoming);
                    switch (interaction)
                    {
                        case MarkInteractionKind.Apply:
                        case MarkInteractionKind.Refresh:
                            markService.ApplyMark(target, rule.mark, attackerId, attackerTurnCounter, rule.mark.baseDurationTurns, executionId);
                            break;
                        case MarkInteractionKind.BlowUp:
                            markService.DetonateMark(target, target.ActiveMark.MarkId, attackerId, null, executionId);
                            break;
                        case MarkInteractionKind.None:
                        default:
                            break;
                    }
                }
            }
        }
    }
}
