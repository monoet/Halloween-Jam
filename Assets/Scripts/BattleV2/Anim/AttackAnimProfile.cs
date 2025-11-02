using UnityEngine;
using DG.Tweening;

namespace BattleV2.Anim
{
    /// <summary>
    /// Perfil de animación para ataque básico: pre-delay, windup, lunge, impacto y recover.
    /// Diseñadores pueden tunear sin tocar código.
    /// </summary>
    [CreateAssetMenu(menuName = "Legacy/BattleV2/Anim/AttackAnimProfile")]
    public class AttackAnimProfile : ScriptableObject
    {
        [Header("PreDelay (Anticipation Flashes)")]
        [Tooltip("Tiempo total del pre-delay antes del windup (si usas flashes).")]
        public float preDelayTotal = 0.15f;
        [Tooltip("Número de flashes durante el pre-delay.")]
        public int preFlashes = 2;
        [Tooltip("Ease del flash (si usas cambio de color/alpha).")]
        public Ease preFlashEase = Ease.InOutSine;
        [Tooltip("Intensidad del flash (0–1 si usas material con _Flash o alpha en SpriteRenderer).")]
        [Range(0f, 1f)] public float preFlashIntensity = 1.0f;

        [Header("Windup (Chargeback)")]
        public float windupBackDist = 0.6f;
        public float windupTime = 0.25f;
        public Ease windupEase = Ease.OutSine;

        [Header("Lunge (Dash In)")]
        public float lungeForwardDist = 1.0f;
        public float lungeTime = 0.12f;
        public Ease lungeEase = Ease.OutExpo;

        [Header("Impact Feedback")]
        [Tooltip("Pequeño freeze para aumentar impacto.")]
        public float hitFreeze = 0.035f;
        [Tooltip("Empuje tras el impacto (knockback corto).")]
        public float hitPushDistance = 0.2f;
        public float hitPushDuration = 0.08f;
        public Ease hitPushEase = Ease.OutQuad;
        public float hitReturnDuration = 0.12f;
        public Ease hitReturnEase = Ease.OutSine;

        [Header("Recover (Return to Idle)")]
        public float recoverTime = 0.22f;
        public Ease recoverEase = Ease.InOutSine;

        public float TotalDuration()
        {
            return preDelayTotal
                 + windupTime
                 + lungeTime
                 + hitFreeze
                 + hitPushDuration
                 + hitReturnDuration
                 + recoverTime;
        }
    }
}
