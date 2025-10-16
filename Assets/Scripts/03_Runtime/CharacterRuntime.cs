using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public struct FinalStats
{
    public int HP, SP;
    public float STR, RES, AGI, LCK, VIT;
    public float Physical, MagicPower, PhysDefense, MagDefense, CritChance, Speed;
}

public class CharacterRuntime : MonoBehaviour
{
    [Header("Archetype (Stat Template)")]
    [SerializeField] private Archetype archetype;

    [Header("Core stats base + bonus")]
    [SerializeField] private CoreStats core;

    [Header("Formulas")]
    [SerializeField] private LinearFormula statFormula;
    [SerializeField] private DerivedFormula derivedFormula;

    [Header("Final Calculated Stats (Inspector)")]
    [SerializeField] private FinalStats final;

    [Header("Runtime Info")]
    public UnityEvent OnStatsChanged;

    public FinalStats Final => final;
    public Archetype Archetype => archetype;
    public CoreStats Core => core;

    private void Awake()
    {
        InitializeFromArchetype();
        Recalc();
    }

    public void Recalc()
    {
        if (statFormula == null)
        {
            Debug.LogWarning(name + ": No LinearFormula assigned!");
            return;
        }

        ApplyGrowthFromArchetype();

        final.STR = statFormula.STR(core);
        final.RES = statFormula.RES(core);
        final.AGI = statFormula.AGI(core);
        final.LCK = statFormula.LCK(core);
        final.VIT = statFormula.VIT(core);

        if (archetype != null)
        {
            final.HP = archetype.GetHPFromVit(final.VIT);
            final.SP = archetype.GetSPFromRes(final.RES);
            if (archetype != null)
            
            {
                final.HP = archetype.GetHPFromVit(final.VIT);
                final.SP = archetype.GetSPFromRes(final.RES);
                Debug.Log($"[Recalc] {archetype.characterName}: VIT={final.VIT:F1} → HP={final.HP} (hpPerVit={archetype.hpPerVit})");
            }


        }

        if (derivedFormula != null)
        {
            var baseStats = final;
            final.Physical    = derivedFormula.AttackPower(baseStats);
            final.MagicPower  = derivedFormula.MagicPower(baseStats);
            final.PhysDefense = derivedFormula.PhysDefense(baseStats);
            final.MagDefense  = derivedFormula.MagDefense(baseStats);
            final.CritChance  = derivedFormula.CritChance(baseStats);
            final.Speed       = derivedFormula.Speed(baseStats);
        }
        else
        {
            final.Physical = final.STR;
            final.MagicPower = final.RES;
            final.PhysDefense = final.VIT;
            final.MagDefense = final.RES;
            final.CritChance = final.LCK * 0.01f;
            final.Speed = final.AGI;
        }

        OnStatsChanged?.Invoke();
    }

    public void AddBonus(float str = 0, float res = 0, float agi = 0, float lck = 0, float vit = 0)
    {
        core.BonusSTR += str;
        core.BonusRES += res;
        core.BonusAGI += agi;
        core.BonusLCK += lck;
        core.BonusVIT += vit;
        Recalc();
    }

    public void RemoveBonus(float str = 0, float res = 0, float agi = 0, float lck = 0, float vit = 0)
    {
        core.BonusSTR -= str;
        core.BonusRES -= res;
        core.BonusAGI -= agi;
        core.BonusLCK -= lck;
        core.BonusVIT -= vit;
        Recalc();
    }

    public int Level => core.Level;
    public float BaseSTR => core.BaseSTR;

    private void InitializeFromArchetype()
    {
        if (archetype == null)
        {
            return;
        }

        if (core.Level <= 0)
        {
            core.Level = 1;
        }

        core.characterName = archetype.characterName;
        core.portrait = archetype.portrait;
    }

    private void ApplyGrowthFromArchetype()
    {
        if (archetype == null)
        {
            return;
        }

        int safeMaxLevel = StatGrowthCalculator.GetSafeMaxLevel(archetype.maxLevel);
        int level = StatGrowthCalculator.ClampLevel(core.Level, safeMaxLevel);

        if (level != core.Level)
        {
            Debug.LogWarning($"{name}: Level clamped from {core.Level} to {level} for archetype {archetype.name}.", this);
            core.Level = level;
        }

        core.BaseSTR = StatGrowthCalculator.CalculateStat(archetype.baseSTR, archetype.strGrowth, level, safeMaxLevel);
        core.BaseRES = StatGrowthCalculator.CalculateStat(archetype.baseRES, archetype.resGrowth, level, safeMaxLevel);
        core.BaseAGI = StatGrowthCalculator.CalculateStat(archetype.baseAGI, archetype.agiGrowth, level, safeMaxLevel);
        core.BaseLCK = StatGrowthCalculator.CalculateStat(archetype.baseLCK, archetype.lckGrowth, level, safeMaxLevel);
        core.BaseVIT = StatGrowthCalculator.CalculateStat(archetype.baseVIT, archetype.vitGrowth, level, safeMaxLevel);
    }

    private static class StatGrowthCalculator
    {
        private const int AbsoluteMaxLevel = 999;

        private static readonly Dictionary<AnimationCurve, CacheEntry> CurveCache = new();

        public static int GetSafeMaxLevel(int archetypeMaxLevel)
        {
            return Mathf.Clamp(archetypeMaxLevel, 1, AbsoluteMaxLevel);
        }

        public static int ClampLevel(int level, int safeMaxLevel)
        {
            return Mathf.Clamp(level, 1, Mathf.Max(1, safeMaxLevel));
        }

        public static float CalculateStat(float baseValue, AnimationCurve curve, int level, int safeMaxLevel)
        {
            if (curve == null)
            {
                return baseValue;
            }

            int clampedLevel = ClampLevel(level, safeMaxLevel);
            float growth = GetCumulativeGrowth(curve, clampedLevel, safeMaxLevel);
            return baseValue + growth;
        }

        private static float GetCumulativeGrowth(AnimationCurve curve, int level, int safeMaxLevel)
        {
            int requiredHash = ComputeCurveHash(curve);

            if (!CurveCache.TryGetValue(curve, out var entry) ||
                entry.CurveHash != requiredHash ||
                entry.MaxLevelComputed < safeMaxLevel)
            {
                entry = BuildEntry(curve, safeMaxLevel, requiredHash);
                CurveCache[curve] = entry;
            }

            int index = Mathf.Clamp(level, 1, entry.Values.Length - 1);
            return entry.Values[index];
        }

        private static CacheEntry BuildEntry(AnimationCurve curve, int safeMaxLevel, int curveHash)
        {
            int length = Mathf.Max(safeMaxLevel + 1, 2);
            var values = new float[length];
            float running = 0f;
            values[0] = 0f;
            values[1] = 0f;

            for (int lvl = 2; lvl < length; lvl++)
            {
                running += curve.Evaluate(lvl);
                values[lvl] = running;
            }

            return new CacheEntry(curveHash, safeMaxLevel, values);
        }

        private static int ComputeCurveHash(AnimationCurve curve)
        {
            unchecked
            {
                int hash = 17;
                foreach (var key in curve.keys)
                {
                    hash = hash * 31 + key.time.GetHashCode();
                    hash = hash * 31 + key.value.GetHashCode();
                    hash = hash * 31 + key.inTangent.GetHashCode();
                    hash = hash * 31 + key.outTangent.GetHashCode();
                    hash = hash * 31 + key.inWeight.GetHashCode();
                    hash = hash * 31 + key.outWeight.GetHashCode();
                    hash = hash * 31 + ((int)key.weightedMode);
                }

                return hash;
            }
        }

        private sealed class CacheEntry
        {
            public CacheEntry(int curveHash, int maxLevelComputed, float[] values)
            {
                CurveHash = curveHash;
                MaxLevelComputed = maxLevelComputed;
                Values = values;
            }

            public int CurveHash { get; }
            public int MaxLevelComputed { get; }
            public float[] Values { get; }
        }
    }
}
