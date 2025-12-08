using System;
using BattleV2.Execution;
using UnityEngine;

namespace BattleV2.Marks
{
    public enum MarkInteractionKind
    {
        None = 0,
        Apply = 1,
        Refresh = 2,
        BlowUp = 3
    }

    public readonly struct MarkElementView
    {
        public MarkElementView(string elementId, bool canBeAppliedAsMark, bool canDetonateMarks)
        {
            ElementId = elementId;
            CanBeAppliedAsMark = canBeAppliedAsMark;
            CanDetonateMarks = canDetonateMarks && !string.IsNullOrWhiteSpace(elementId);
        }

        public string ElementId { get; }
        public bool CanBeAppliedAsMark { get; }
        public bool CanDetonateMarks { get; }
        public bool IsNone => string.IsNullOrWhiteSpace(ElementId);

        public static MarkElementView FromDefinition(MarkDefinition def)
        {
            if (def == null)
            {
                return new MarkElementView(null, false, false);
            }

            var elementId = !string.IsNullOrWhiteSpace(def.elementId) ? def.elementId : def.id;
            return new MarkElementView(elementId, def.canBeAppliedAsMark, def.canDetonateMarks);
        }
    }

    /// <summary>
    /// Reglas puras (sin side-effects) para el minijuego de Marks.
    /// </summary>
    public static class MarkRulesEngine
    {
        private const float BaseAoeChance = 0.25f;

        public static bool QualifiesForMarksSingle(int cpSpent, TimedGrade timedGrade, MarkElementView incoming)
        {
            if (cpSpent < 1)
            {
                return false;
            }

            if (incoming.IsNone || !incoming.CanDetonateMarks)
            {
                return false;
            }

            return timedGrade >= TimedGrade.Success;
        }

        public static bool QualifiesForMarksAoETarget(
            int cpSpent,
            MarkElementView incoming,
            float perCpBonus,
            int rngSeed,
            out float chance,
            out float roll)
        {
            chance = 0f;
            roll = 0f;

            if (cpSpent < 1)
            {
                return false;
            }

            if (incoming.IsNone || !incoming.CanDetonateMarks)
            {
                return false;
            }

            chance = Mathf.Clamp01(BaseAoeChance + cpSpent * perCpBonus);
            var rng = new System.Random(rngSeed);
            roll = (float)rng.NextDouble();
            return roll <= chance;
        }

        public static MarkInteractionKind ResolveInteraction(MarkSlot activeMark, MarkElementView incoming)
        {
            if (incoming.IsNone || !incoming.CanDetonateMarks)
            {
                return MarkInteractionKind.None;
            }

            if (!activeMark.HasValue)
            {
                return incoming.CanBeAppliedAsMark ? MarkInteractionKind.Apply : MarkInteractionKind.None;
            }

            var incomingId = Normalize(incoming.ElementId);
            var activeId = Normalize(ResolveElementId(activeMark));

            if (string.Equals(activeId, incomingId, StringComparison.Ordinal))
            {
                return MarkInteractionKind.Refresh;
            }

            return MarkInteractionKind.BlowUp;
        }

        private static string ResolveElementId(MarkSlot slot)
        {
            var def = slot.Definition;
            if (def != null && !string.IsNullOrWhiteSpace(def.elementId))
            {
                return def.elementId;
            }

            return slot.MarkId;
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
