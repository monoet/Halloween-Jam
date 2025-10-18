using UnityEngine;
using DG.Tweening;

namespace BattleV2.Anim
{
    /// <summary>
    /// Scriptable profile that defines the timing and distances for the basic attack loop.
    /// Designers can tweak these values per combatant without touching code.
    /// </summary>
    [CreateAssetMenu(menuName = "BattleV2/Anim/AttackAnimProfile")]
    public class AttackAnimProfile : ScriptableObject
    {
        [Header("Windup (Chargeback)")]
        public float windupBackDist = 0.6f;
        public float windupTime = 0.25f;
        public Ease windupEase = Ease.OutSine;

        [Header("Lunge (Dash In)")]
        public float lungeForwardDist = 1.0f;
        public float lungeTime = 0.12f;
        public Ease lungeEase = Ease.OutExpo;

        [Header("Impact Feedback")]
        public float hitFreeze = 0.035f;
        public float enemyHitShakeTime = 0.18f;
        public float enemyHitShakeStrength = 0.25f;

        [Header("Recover (Return to Idle)")]
        public float recoverTime = 0.22f;
        public Ease recoverEase = Ease.InOutSine;

        public float TotalDuration()
        {
            return windupTime + lungeTime + hitFreeze + recoverTime;
        }
    }
}
