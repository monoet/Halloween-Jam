using UnityEngine;

/// <summary>
/// Defines combat action parameters for rapid iteration.
/// </summary>
[CreateAssetMenu(fileName = "ActionData", menuName = "Combat/Action Data")]
public class ActionData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string actionId;
    [SerializeField] private string actionName;

    [Header("Costs")]
    [SerializeField] private int spCost;
    [SerializeField] private int cpCost;

    [Header("Damage Scaling")]
    [SerializeField] private float basePower = 10f;
    [SerializeField] private float strScaling = 1f;
    [SerializeField] private float resScaling;
    [SerializeField] private float agiScaling;
    [SerializeField] private float lckScaling;
    [SerializeField] private float vitScaling;
    [SerializeField] private float physicalScaling;
    [SerializeField] private float magicScaling;

    [Header("Metadata")]
    [SerializeField, TextArea] private string description;

    [Header("Combo Points (CP)")]
    [SerializeField, Min(0)] private int cpGain;

    [Header("Accuracy")]
    [SerializeField, Range(0f, 1f)] private float hitChance = 1f;

    [Header("Timed Hit")]
    [SerializeField] private bool isTimedHit = false;
    [SerializeField, Range(0.1f, 3f)] private float timedDuration = 1f;
    [SerializeField, Range(0f, 1f)] private float timedTarget = 0.5f;
    [SerializeField, Range(0f, 0.5f)] private float timedPerfectWindow = 0.06f;
    [SerializeField, Range(0f, 0.5f)] private float timedGoodWindow = 0.15f;
    [SerializeField, Range(0f, 3f)] private float perfectMultiplier = 1.5f;
    [SerializeField, Range(0f, 3f)] private float goodMultiplier = 1.2f;
    [SerializeField, Range(0f, 1f)] private float missMultiplier = 0f;

    public string ActionId => actionId;
    public string ActionName => actionName;
    public int SpCost => spCost;
    public int CpCost => cpCost;
    public int CpGain => cpGain;
    public float HitChance => hitChance;
    public bool IsTimedHit => isTimedHit;
    public float TimedDuration => timedDuration;
    public float TimedTarget => timedTarget;
    public float TimedPerfectWindow => timedPerfectWindow;
    public float TimedGoodWindow => timedGoodWindow;
    public float PerfectMultiplier => perfectMultiplier;
    public float GoodMultiplier => goodMultiplier;
    public float MissMultiplier => missMultiplier;
    public float BasePower => basePower;
    public float StrScaling => strScaling;
    public float ResScaling => resScaling;
    public float AgiScaling => agiScaling;
    public float LckScaling => lckScaling;
    public float VitScaling => vitScaling;
    public float PhysicalScaling => physicalScaling;
    public float MagicScaling => magicScaling;
    public string Description => description;
}
