using System.Collections.Generic;
using BattleV2.Core;
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

            IReadOnlyList<CombatantState> orderedTargets = targets;
            if (isAoE && targets.Count > 1)
            {
                var list = new List<CombatantState>(targets.Count);
                var seen = new HashSet<int>();
                for (int t = 0; t < targets.Count; t++)
                {
                    var target = targets[t];
                    if (target != null)
                    {
                        if (!seen.Add(target.StableId))
                        {
                            BattleDiagnostics.Log(
                                "Marks.RNG",
                                $"Duplicate StableId detected in AoE targets. stableId={target.StableId} exec={executionId}",
                                attacker);
                        }

                        list.Add(target);
                    }
                }
                list.Sort((a, b) =>
                {
                    if (a == null && b == null) return 0;
                    if (a == null) return 1;
                    if (b == null) return -1;
                    int cmp = a.StableId.CompareTo(b.StableId);
                    return cmp;
                });
                orderedTargets = list;
            }

            for (int i = 0; i < orderedTargets.Count; i++)
            {
                var target = orderedTargets[i];
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

                    float chance = 0f;
                    float roll = 0f;
                    bool qualifies = isAoE
                        ? MarkRulesEngine.QualifiesForMarksAoETarget(judgment.CpSpent, incoming, aoePerCpBonus, targetJudgment.RngSeed, out chance, out roll)
                        : MarkRulesEngine.QualifiesForMarksSingle(judgment.CpSpent, timedGrade, incoming);

                    if (isAoE)
                    {
                        // Traza para reproducibilidad: seed por target + chance/roll
                        BattleDiagnostics.Log(
                            "Marks.RNG",
                            $"exec={executionId} attacker={attackerId} target={target?.StableId ?? 0} seed={targetJudgment.RngSeed} chance={chance:F3} roll={roll:F3} qualifies={qualifies}",
                            attacker);
                    }

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
