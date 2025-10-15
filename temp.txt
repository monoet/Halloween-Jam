using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "RPG/Archetype")]
public class Archetype : ScriptableObject
{
    [Header("Identidad del Personaje")]
    [Tooltip("ID único para guardado y referencia")]
    public string characterID = System.Guid.NewGuid().ToString();
    public string characterName;
    public Sprite portrait;
    [TextArea(2, 4)] public string description;

    [Header("Clase principal")]
    [Tooltip("Referencia a la clase base (controla growth rates globales)")]
    public ClassMastery classMastery;

    [Header("Afinidad elemental / temática")]
    public ElementalAffinity affinity = ElementalAffinity.None;
    public Color auraColor = Color.white; // FX / UI / partículas

    // ─────────────────────────────
    // STATS BASE
    // ─────────────────────────────
    [Header("Stats base (nivel 1)")]
    public float baseSTR = 10; // Fuerza
    public float baseRES = 10; // Resonancia (afecta SP y poder mágico)
    public float baseAGI = 10; // Agilidad
    public float baseLCK = 10; // Suerte
    public float baseVIT = 10; // Vitalidad

    // ─────────────────────────────
    // CURVAS DE CRECIMIENTO
    // ─────────────────────────────
    [Header("Curvas de crecimiento por stat base")]
    [Range(1, 99)] public int maxLevel = 50;

    public AnimationCurve strGrowth = AnimationCurve.Linear(1, 10, 50, 99);
    public AnimationCurve resGrowth = AnimationCurve.Linear(1, 10, 50, 99);
    public AnimationCurve agiGrowth = AnimationCurve.Linear(1, 10, 50, 99);
    public AnimationCurve lckGrowth = AnimationCurve.Linear(1, 10, 50, 99);
    public AnimationCurve vitGrowth = AnimationCurve.Linear(1, 10, 50, 99);

    // ─────────────────────────────
    // RELACIONES DERIVADAS
    // ─────────────────────────────
    [Header("Derivados: relación de HP y SP con stats base")]
    [Tooltip("Multiplicador de HP por punto de Vitalidad")]
    public float hpPerVit = 20f;

    [Tooltip("Multiplicador de SP por punto de Resonancia")]
    public float spPerRes = 8f;

    [Tooltip("Curva global de rendimiento (simula softcaps tipo Souls)")]
    public AnimationCurve gainCurve = new AnimationCurve(
        new Keyframe(1, 0.6f),
        new Keyframe(20, 1.0f),
        new Keyframe(30, 0.8f),
        new Keyframe(40, 0.6f),
        new Keyframe(50, 0.4f)
    );

    // ─────────────────────────────
    // INICIALIZACIÓN
    // ─────────────────────────────
    [Header("Inicialización")]
    public List<AbilityData> startingAbilities = new();
    public List<EquipmentData> startingEquipment = new();

    [Header("Referencias visuales y FX (opcional)")]
    public GameObject battlePrefab;
    public GameObject overworldPrefab;

    // ─────────────────────────────
    // MÉTODOS DE CÁLCULO
    // ─────────────────────────────

    public int GetHPFromVit(float vit)
    {
        float curve = gainCurve.Evaluate(vit);
        return Mathf.RoundToInt(vit * hpPerVit * curve);
    }

    public int GetSPFromRes(float res)
    {
        float curve = gainCurve.Evaluate(res);
        return Mathf.RoundToInt(res * spPerRes * curve);
    }

    public float GetStatValue(AnimationCurve curve, float baseValue, int level)
    {
        return baseValue + curve.Evaluate(level);
    }
}
