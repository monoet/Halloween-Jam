using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HalloweenJam.UI.Combat
{
    /// <summary>
    /// Lightweight feedback helper for a BattleHUD.
    /// Provides placeholder visual cues (floating text, shake, CP flash).
    /// </summary>
    public sealed class BattleHUDFeedback : MonoBehaviour
    {
        [Header("Shake")]
        [SerializeField] private RectTransform shakeTarget;
        [SerializeField] private float shakeDuration = 0.15f;
        [SerializeField] private float shakeMagnitude = 8f;

        [Header("Floating Text")]
        [SerializeField] private TMP_Text floatingTextPrefab;
        [SerializeField] private Transform floatingTextRoot;
        [SerializeField] private Color damageColor = Color.red;
        [SerializeField] private Color healColor = Color.green;
        [SerializeField] private Color cpColor = Color.cyan;

        [Header("CP Flash")]
        [SerializeField] private Graphic cpHighlightGraphic;
        [SerializeField] private Color cpGainColor = Color.cyan;
        [SerializeField] private Color cpSpendColor = new Color(1f, 0.6f, 0f);
        [SerializeField] private float cpFlashDuration = 0.25f;

        private Coroutine shakeRoutine;
        private Coroutine cpFlashRoutine;

        public void PlayDamage(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            SpawnFloatingText($"-{amount}", damageColor);
            TriggerShake();
        }

        public void PlayHeal(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            SpawnFloatingText($"+{amount}", healColor);
        }

        public void PlayCpChange(int delta)
        {
            if (delta == 0)
            {
                return;
            }

            SpawnFloatingText(delta > 0 ? $"+{delta} CP" : $"{delta} CP", cpColor);
            TriggerCpFlash(delta > 0 ? cpGainColor : cpSpendColor);
        }

        public void PlayChargeCue()
        {
            TriggerMinorShake();
        }

        public void PlayLungeCue()
        {
            TriggerMinorShake();
        }

        public void PlayImpactCue()
        {
            TriggerShake();
        }

        public void PlayIncomingImpactCue()
        {
            TriggerShake();
        }

        public void PlayRecoverCue()
        {
            // Placeholder for future polish; currently no additional feedback.
        }

        private void TriggerShake()
        {
            if (shakeTarget == null)
            {
                return;
            }

            if (shakeRoutine != null)
            {
                StopCoroutine(shakeRoutine);
            }

            shakeRoutine = StartCoroutine(ShakeRoutine());
        }

        private IEnumerator ShakeRoutine()
        {
            var originalAnchoredPosition = shakeTarget.anchoredPosition;
            float elapsed = 0f;

            while (elapsed < shakeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float offsetX = Random.Range(-shakeMagnitude, shakeMagnitude);
                float offsetY = Random.Range(-shakeMagnitude, shakeMagnitude);
                shakeTarget.anchoredPosition = originalAnchoredPosition + new Vector2(offsetX, offsetY);
                yield return null;
            }

            shakeTarget.anchoredPosition = originalAnchoredPosition;
            shakeRoutine = null;
        }

        private void TriggerMinorShake()
        {
            if (shakeTarget == null)
            {
                return;
            }

            if (shakeRoutine != null)
            {
                StopCoroutine(shakeRoutine);
            }

            shakeRoutine = StartCoroutine(MinorShakeRoutine());
        }

        private IEnumerator MinorShakeRoutine()
        {
            var originalAnchoredPosition = shakeTarget.anchoredPosition;
            float elapsed = 0f;
            float duration = Mathf.Max(0.05f, shakeDuration * 0.5f);
            float magnitude = shakeMagnitude * 0.4f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float offsetX = Random.Range(-magnitude, magnitude);
                float offsetY = Random.Range(-magnitude, magnitude);
                shakeTarget.anchoredPosition = originalAnchoredPosition + new Vector2(offsetX, offsetY);
                yield return null;
            }

            shakeTarget.anchoredPosition = originalAnchoredPosition;
            shakeRoutine = null;
        }

        private void TriggerCpFlash(Color color)
        {
            if (cpHighlightGraphic == null)
            {
                return;
            }

            if (cpFlashRoutine != null)
            {
                StopCoroutine(cpFlashRoutine);
            }

            cpFlashRoutine = StartCoroutine(CpFlashRoutine(color));
        }

        private IEnumerator CpFlashRoutine(Color color)
        {
            var originalColor = cpHighlightGraphic.color;
            float elapsed = 0f;

            while (elapsed < cpFlashDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / cpFlashDuration);
                cpHighlightGraphic.color = Color.Lerp(color, originalColor, t);
                yield return null;
            }

            cpHighlightGraphic.color = originalColor;
            cpFlashRoutine = null;
        }

        private void SpawnFloatingText(string message, Color color)
        {
            if (floatingTextPrefab == null)
            {
                Debug.LogFormat("[BattleHUDFeedback] {0}", message);
                return;
            }

            var targetRoot = floatingTextRoot != null ? floatingTextRoot : transform;
            var instance = Instantiate(floatingTextPrefab, targetRoot);
            instance.text = message;
            instance.color = color;

            StartCoroutine(FadeAndDestroy(instance));
        }

        private static IEnumerator FadeAndDestroy(TMP_Text text)
        {
            const float duration = 0.6f;
            float elapsed = 0f;
            var originalColor = text.color;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = Mathf.Clamp01(1f - (elapsed / duration));
                var color = originalColor;
                color.a = alpha;
                text.color = color;
                text.rectTransform.anchoredPosition += Vector2.up * 20f * Time.unscaledDeltaTime;
                yield return null;
            }

            Destroy(text.gameObject);
        }
    }
}
