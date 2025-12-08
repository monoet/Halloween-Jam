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
        private readonly IMarkReactionResolver reactionResolver;

        public MarkInteractionProcessor(MarkService markService, float aoePerCpBonus = 0.1f, IMarkReactionResolver reactionResolver = null)
        {
            this.markService = markService;
            this.aoePerCpBonus = aoePerCpBonus;
            this.reactionResolver = reactionResolver;
        }

        public void Process(
            CombatantState attacker,
            BattleSelection selection,
            ActionJudgment judgment,
            IReadOnlyList<CombatantState> targets,
            int executionId,
            int attackerTurnCounter = 0,
            string axisSubtype = "")
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
                    string reactionId = null;
                    ReactionKey reactionKey = ReactionKey.None;
                    if (interaction == MarkInteractionKind.BlowUp)
                    {
                        reactionKey = ReactionKey.From(target.ActiveMark, incoming, axisSubtype);
                        reactionResolver?.TryResolveId(reactionKey, out reactionId);
                    }

                    switch (interaction)
                    {
                        case MarkInteractionKind.Apply:
                        case MarkInteractionKind.Refresh:
                            markService.ApplyMark(target, rule.mark, attackerId, attackerTurnCounter, rule.mark.baseDurationTurns, executionId);
                            break;
                        case MarkInteractionKind.BlowUp:
                            markService.DetonateMark(target, target.ActiveMark.MarkId, attackerId, reactionId, executionId);
                            if (!string.IsNullOrEmpty(reactionId))
                            {
                                reactionResolver?.Execute(new MarkReactionContext(attacker, target, reactionKey, executionId), reactionId);
                            }
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
