using BattleV2.Execution.TimedHits;
using BattleV2.UI;
using UnityEngine;

namespace BattleV2.Anim
{
    /// <summary>
    /// Bridges timed-hit feedback events to presentation elements (damage numbers, impact FX).
    /// </summary>
    public sealed class TimedHitFeedbackRouter : MonoBehaviour
    {
        [Header("Damage Numbers")]
        [SerializeField] private FloatingDamageText damageTextPrefab;
        [SerializeField] private Transform damageNumberRoot;
        [SerializeField] private Vector3 damageNumberOffset = new Vector3(0f, 1.25f, 0f);

        [Header("Fallbacks")]
        [SerializeField] private bool logMissingTargets = true;

        private void OnEnable()
        {
            BattleEvents.OnTimedHitPhaseFeedback += HandlePhaseFeedback;
        }

        private void OnDisable()
        {
            BattleEvents.OnTimedHitPhaseFeedback -= HandlePhaseFeedback;
        }

        private void HandlePhaseFeedback(TimedHitPhaseFeedback feedback)
        {
            if (feedback.Damage <= 0)
            {
                return;
            }

            SpawnDamageNumber(feedback);
        }

        private void SpawnDamageNumber(TimedHitPhaseFeedback feedback)
        {
            if (damageTextPrefab == null)
            {
                return;
            }

            var target = feedback.Target;
            if (target == null)
            {
                if (logMissingTargets)
                {
                    Debug.LogWarning("[TimedHitFeedbackRouter] Missing target on feedback; cannot spawn damage number.");
                }
                return;
            }

            Vector3 basePosition = feedback.WorldPosition ?? target.transform.position;
            Vector3 spawnPosition = basePosition + damageNumberOffset;
            Transform parent = damageNumberRoot != null ? damageNumberRoot : null;

            var text = Instantiate(damageTextPrefab, spawnPosition, Quaternion.identity, parent);
            text.Initialise(feedback.Damage, isHealing: false);
        }
    }
}
