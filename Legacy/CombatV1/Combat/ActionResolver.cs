using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Resolves combat actions using the migrated stats system.
/// Can be plugged into turn flow to apply damage, VFX, CP costs, etc.
/// </summary>
public sealed class ActionResolver : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool logDetailedCalculations = true;
    [SerializeField] private bool enforceCpCost = false;

    [Header("VFX Controller (optional)")]
    [SerializeField] private CombatVfxController vfx;

    [Header("Crit Settings")]
    [SerializeField, Min(1f)] private float critMultiplier = 1.5f;
    [SerializeField] private bool testingAlwaysCrit = false;
    [SerializeField] private bool testingForceNextCrit = false;

    [System.Serializable]
    public sealed class ActionEvent : UnityEvent { }

    [Header("Events")]
    public ActionEvent OnActionComplete = new ActionEvent();

    public void ExecuteAction(CharacterRuntime actor, ActionData action, CharacterRuntime target, float damageMultiplier = 1f)
    {
        if (actor == null || action == null || target == null)
        {
            Debug.LogWarning("[ActionResolver] Invalid parameters for ExecuteAction.");
            return;
        }

        if (vfx == null)
        {
            vfx = FindObjectOfType<CombatVfxController>(includeInactive: true);
        }

        float finalPower = CalculateActionPower(actor, action, logDetailedCalculations, this) * Mathf.Max(0f, damageMultiplier);
        bool isCrit = !action.IsTimedHit && ComputeCrit(actor);
        if (isCrit)
        {
            finalPower *= critMultiplier;
            Debug.Log($"[ActionResolver] CRIT x{critMultiplier:0.00}: {actor.DisplayName()} → {target.DisplayName()} | Power {finalPower:0.00}");
        }
        else
        {
            Debug.Log($"[ActionResolver] {actor.DisplayName()} usa {action.ActionName} sobre {target.DisplayName()} | Power {finalPower:0.00}");
        }

        var actorState = EnsureCombatantState(actor);
        var targetState = EnsureCombatantState(target);

        if (action.CpCost > 0)
        {
            if (enforceCpCost && !actorState.SpendCP(action.CpCost))
            {
                Debug.LogWarning($"[ActionResolver] Acción {action.ActionName} cancelada: CP insuficientes.");
                return;
            }

            if (!enforceCpCost)
            {
                actorState.SpendCP(action.CpCost);
            }
        }

        Sequence feedbackSequence = PlayActionFeedback(actor.transform, target.transform);

        float chance = Mathf.Clamp01(action.HitChance);
        bool isHit = Random.value <= chance && damageMultiplier > 0f;
        if (!isHit)
        {
            ShowMiss(target);
            CompleteSequence(feedbackSequence);
            return;
        }

        int damage = Mathf.Max(1, Mathf.CeilToInt(finalPower));
        targetState.TakeDamage(damage);

        ShowDamage(target, damage, isCrit);

        if (action.CpGain > 0)
        {
            actorState.AddCP(action.CpGain);
        }

        CompleteSequence(feedbackSequence);
    }

    private CombatantState EnsureCombatantState(CharacterRuntime character)
    {
        if (character == null)
        {
            return null;
        }

        var state = character.GetComponent<CombatantState>();
        if (state == null)
        {
            state = character.gameObject.AddComponent<CombatantState>();
        }

        state.EnsureInitialized(character);
        return state;
    }

    private Sequence PlayActionFeedback(Transform actor, Transform target)
    {
        if (vfx == null)
        {
            return null;
        }

        var attackTween = vfx.PlayAttackFeedback(actor, target);
        if (!vfx.SpawnProjectileOnAction)
        {
            return attackTween;
        }

        var projectile = vfx.PlayProjectileShot(actor, target);
        if (projectile == null && attackTween == null)
        {
            return null;
        }

        var sequence = DOTween.Sequence();
        if (projectile != null)
        {
            sequence.Append(projectile);
        }

        if (attackTween != null)
        {
            sequence.Join(attackTween);
        }

        return sequence;
    }

    private void CompleteSequence(Sequence sequence)
    {
        if (sequence != null)
        {
            sequence.OnComplete(() => OnActionComplete.Invoke());
        }
        else
        {
            OnActionComplete.Invoke();
        }
    }

    private void ShowMiss(CharacterRuntime target)
    {
        if (vfx == null || target == null)
        {
            return;
        }

        var pos = target.transform.position + Vector3.up * 1.2f;
        vfx.ShowMiss(pos);
    }

    private void ShowDamage(CharacterRuntime target, int damage, bool isCrit)
    {
        if (vfx == null || target == null)
        {
            return;
        }

        var pos = target.transform.position + Vector3.up * 1.2f;
        vfx.ShowDamageNumber(damage, pos, isHeal: false, isCrit: isCrit);
    }

    private bool ComputeCrit(CharacterRuntime actor)
    {
        if (testingAlwaysCrit || testingForceNextCrit)
        {
            testingForceNextCrit = false;
            return true;
        }

        float chance = actor != null ? actor.Final.CritChance : 0f;
        if (chance > 1f && chance <= 100f)
        {
            chance *= 0.01f;
        }

        chance = Mathf.Clamp01(chance);
        return RollCritStatic(actor);
    }

    [ContextMenu("Force Next Crit (Testing)")]
    private void ForceNextCrit()
    {
        testingForceNextCrit = true;
        Debug.Log("[ActionResolver] Próximo ataque será crítico (testing).");
    }

    internal static float CalculateActionPower(CharacterRuntime actor, ActionData action, bool logDetails, Object debugContext = null)
    {
        if (actor == null || action == null)
        {
            return 0f;
        }

        float power = action.BasePower;
        FinalStats stats = actor.Final;

        power += stats.STR * action.StrScaling;
        power += stats.RES * action.ResScaling;
        power += stats.AGI * action.AgiScaling;
        power += stats.LCK * action.LckScaling;
        power += stats.VIT * action.VitScaling;
        power += stats.MagicPower * action.MagicScaling;
        power += stats.Physical * action.PhysicalScaling;

        if (logDetails)
        {
            Debug.Log($"[ActionResolver] Scaling -> STR:{stats.STR:0.0} RES:{stats.RES:0.0} AGI:{stats.AGI:0.0} LCK:{stats.LCK:0.0} VIT:{stats.VIT:0.0} Phys:{stats.Physical:0.0} Mag:{stats.MagicPower:0.0}", debugContext);
        }

        return power;
    }

    internal static bool RollCritStatic(CharacterRuntime actor)
    {
        float chance = actor != null ? actor.Final.CritChance : 0f;
        if (chance > 1f && chance <= 100f)
        {
            chance *= 0.01f;
        }

        chance = Mathf.Clamp01(chance);
        return Random.value < chance;
    }
}

internal static class CharacterRuntimeExtensions
{
    public static string DisplayName(this CharacterRuntime runtime)
    {
        if (runtime == null)
        {
            return "<null>";
        }

        if (!string.IsNullOrWhiteSpace(runtime.Core.characterName))
        {
            return runtime.Core.characterName;
        }

        if (runtime.Archetype != null && !string.IsNullOrWhiteSpace(runtime.Archetype.characterName))
        {
            return runtime.Archetype.characterName;
        }

        return runtime.name;
    }
}
