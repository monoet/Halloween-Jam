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
        if (archetype != null)
        {
            core.Level = 1;

            core.BaseSTR = archetype.baseSTR;
            core.BaseRES = archetype.baseRES;
            core.BaseAGI = archetype.baseAGI;
            core.BaseLCK = archetype.baseLCK;
            core.BaseVIT = archetype.baseVIT;

            core.characterName = archetype.characterName;
            core.portrait = archetype.portrait;
        }

        Recalc();
    }

    public void Recalc()
    {
        if (statFormula == null)
        {
            Debug.LogWarning(name + ": No LinearFormula assigned!");
            return;
        }

        final.STR = statFormula.STR(core);
        final.RES = statFormula.RES(core);
        final.AGI = statFormula.AGI(core);
        final.LCK = statFormula.LCK(core);
        final.VIT = statFormula.VIT(core);

        if (archetype != null)
        {
            final.HP = archetype.GetHPFromVit(final.VIT);
            final.SP = archetype.GetSPFromRes(final.RES);
        }

        if (derivedFormula != null)
        {
            final.Physical    = derivedFormula.AttackPower(core);
            final.MagicPower  = derivedFormula.MagicPower(core);
            final.PhysDefense = derivedFormula.PhysDefense(core);
            final.MagDefense  = derivedFormula.MagDefense(core);
            final.CritChance  = derivedFormula.CritChance(core);
            final.Speed       = derivedFormula.Speed(core);
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
}

