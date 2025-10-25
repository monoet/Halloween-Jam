using BattleV2.Execution.TimedHits;
using BattleV2.UI;
using UnityEngine;
using UnityEngine.UI;

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
            Transform parent = null;
            if (damageNumberRoot != null && damageNumberRoot.gameObject.scene.IsValid())
            {
                parent = damageNumberRoot;
            }

            if (TrySpawnInCanvas(spawnPosition, parent, out var instance))
            {
                instance.Initialise(feedback.Damage, isHealing: false);
                return;
            }

            var text = Instantiate(damageTextPrefab, spawnPosition, Quaternion.identity, parent);
            text.Initialise(feedback.Damage, isHealing: false);
        }

        private bool TrySpawnInCanvas(Vector3 worldPosition, Transform parent, out FloatingDamageText instance)
        {
            instance = null;

            if (parent == null)
            {
                return false;
            }

            var rectParent = parent as RectTransform ?? parent.GetComponent<RectTransform>();
            if (rectParent == null)
            {
                return false;
            }

            var canvas = rectParent.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace)
            {
                return false;
            }

            Camera worldCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? Camera.main
                : (canvas.worldCamera != null ? canvas.worldCamera : Camera.main);

            if (worldCamera == null)
            {
                return false;
            }

            Vector3 screenPoint = worldCamera.WorldToScreenPoint(worldPosition);
            Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : worldCamera;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectParent, screenPoint, eventCamera, out var anchored))
            {
                return false;
            }

            instance = Instantiate(damageTextPrefab, rectParent);
            var rect = instance.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = anchored;
            }

            return true;
        }
    }
}
