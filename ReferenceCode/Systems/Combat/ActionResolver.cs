using UnityEngine;
using DG.Tweening;

/// <summary>
/// Resuelve los efectos de las acciones. Placeholder para cálculos reales.
/// </summary>
    public class ActionResolver : MonoBehaviour
    {
    [Header("Configuración de debug")]
    [SerializeField] private bool logDetailedCalculations = true;
    [SerializeField] private bool enforceCPCost = false;

    [Header("VFX Controller (opcional)")]
    [SerializeField] private CombatVfxController vfx;

    [Header("Crit Settings")]
    [SerializeField, Min(1f)] private float critMultiplier = 1.5f;
    [SerializeField] private bool testingAlwaysCrit = false;
    [SerializeField] private bool testingForceNextCrit = false;

    public void ExecuteAction(CharacterRuntime actor, ActionData action, CharacterRuntime target, float damageMultiplier = 1f)
    {
        if (actor == null || action == null || target == null)
        {
            Debug.LogWarning("[ActionResolver] Parámetros inválidos para ejecutar acción.");
            return;
        }

        if (vfx == null)
            vfx = FindObjectOfType<CombatVfxController>(true);

        float finalPower = CalculateActionPower(actor, action);
        finalPower *= Mathf.Max(0f, damageMultiplier);
        bool isCrit = action.IsTimedHit ? false : ComputeCrit(actor);
        if (isCrit)
        {
            finalPower *= critMultiplier;
            Debug.Log($"[ActionResolver] CRIT! x{critMultiplier:0.00} {actor.Archetype.characterName} usa {action.ActionName} sobre {target.Archetype.characterName}. Power: {finalPower}");
        }
        else
        {
            Debug.Log($"[ActionResolver] {actor.Archetype.characterName} usa {action.ActionName} sobre {target.Archetype.characterName}. Power: {finalPower}");
        }

        // CP: costos y ganancias
        var actorState = actor.GetComponent<CombatantState>();
        if (actorState == null)
        {
            actorState = actor.gameObject.AddComponent<CombatantState>();
            actorState.InitializeFrom(actor);
        }

        if (action.CpCost > 0)
        {
            if (enforceCPCost && !actorState.SpendCP(action.CpCost))
            {
                Debug.LogWarning($"[ActionResolver] Acción {action.ActionName} cancelada: CP insuficientes.");
                return;
            }
            else if (!enforceCPCost)
            {
                // Gastar si alcanza; si no, continuar igual (modo sandbox)
                actorState.SpendCP(action.CpCost);
            }
        }

                // Visual feedback (no bloqueante)
        DG.Tweening.Sequence seq = null;
        if (vfx != null)
        {
            var sAtk = vfx.PlayAttackFeedback(actor.transform, target.transform);
            if (vfx.SpawnProjectileOnAction)
            {
                var sProj = vfx.PlayProjectileShot(actor.transform, target.transform);
                seq = DG.Tweening.DOTween.Sequence();
                seq.Append(sProj);
                if (sAtk != null) seq.Join(sAtk);
            }
            else
            {
                seq = sAtk;
            }
        }

        // Accuracy / MISS check (antes de aplicar da�o) o multiplier 0
        float chance = Mathf.Clamp01(action.HitChance);
        bool isHit = Random.value <= chance;
        if (!isHit || damageMultiplier <= 0f)
        {
            if (vfx != null)
            {
                var mpos = target.transform.position + Vector3.up * 1.2f;
                vfx.ShowMiss(mpos);
            }
            if (seq != null)
                seq.OnComplete(() => OnActionComplete.Invoke());
            else
                OnActionComplete.Invoke();
            return;
        }// Aplicar daño simple al objetivo
        var targetState = target.GetComponent<CombatantState>();
        if (targetState == null)
        {
            targetState = target.gameObject.AddComponent<CombatantState>();
            targetState.InitializeFrom(target);
        }

        int damage = Mathf.Max(1, Mathf.CeilToInt(finalPower));
        targetState.TakeDamage(damage);

        // Floating damage text (si hay VFX controller)
        if (vfx != null)
        {
            var pos = target.transform.position + Vector3.up * 1.2f;
            vfx.ShowDamageNumber(damage, pos, isHeal: false, isCrit: isCrit);
        }

        if (action.CpGain > 0)
            actorState.AddCP(action.CpGain);

        // TODO: Costos SP, estados alterados, resistencias, críticos.

        if (seq != null)
            seq.OnComplete(() => OnActionComplete.Invoke());
        else
            OnActionComplete.Invoke();
    }

    [System.Serializable]
    public class ActionEvent : UnityEngine.Events.UnityEvent { }
    [Header("Eventos")]
    public ActionEvent OnActionComplete = new ActionEvent();

    private bool ComputeCrit(CharacterRuntime actor)
    {
        if (testingAlwaysCrit || testingForceNextCrit)
        {
            testingForceNextCrit = false; // consume flag de siguiente golpe
            return true;
        }
        float chance = actor != null ? actor.Final.CritChance : 0f;
        if (chance > 1f && chance <= 100f) chance *= 0.01f; // soporta porcentaje 0-100
        chance = Mathf.Clamp01(chance);
        return Random.value < chance;
    }

    [ContextMenu("Force Next Crit (Testing)")]
    public void ForceNextCrit()
    {
        testingForceNextCrit = true;
        Debug.Log("[ActionResolver] Próximo ataque será CRIT (testing)");
    }

    private float CalculateActionPower(CharacterRuntime actor, ActionData action)
    {
        float power = action.BasePower;
        FinalStats stats = actor.Final;

        power += stats.STR * action.StrScaling;
        power += stats.RES * action.ResScaling;
        power += stats.AGI * action.AgiScaling;
        power += stats.LCK * action.LckScaling;
        power += stats.VIT * action.VitScaling;
        power += stats.MagicPower * action.MagicScaling;
        power += stats.Physical * action.PhysicalScaling;

        if (logDetailedCalculations)
        {
            Debug.Log($"[ActionResolver] Detalle escalado -> STR:{stats.STR} RES:{stats.RES} AGI:{stats.AGI} LCK:{stats.LCK} VIT:{stats.VIT} Phys:{stats.Physical} Mag:{stats.MagicPower}");
        }

        return power;
    }
}





