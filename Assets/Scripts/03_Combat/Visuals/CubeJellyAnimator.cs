using DG.Tweening;
using UnityEngine;

namespace HalloweenJam.Visuals
{
    public class CubeJellyAnimator : MonoBehaviour
    {
        [Header("Hop Heights")]
        [SerializeField] private float smallHopHeight = 0.5f;
        [SerializeField] private float bigHopHeight = 1.2f;

        [Header("Delays")]
        [SerializeField] private float hopDelay = 0.2f;
        [SerializeField] private float hopDelayVariance = 0.05f;

        [Header("Scale Profiles")]
        [SerializeField] private Vector3 smallSquashScale = new Vector3(1.15f, 0.85f, 1.15f);
        [SerializeField] private Vector3 smallStretchScale = new Vector3(0.9f, 1.2f, 0.9f);
        [SerializeField] private Vector3 largeSquashScale = new Vector3(1.3f, 0.7f, 1.3f);
        [SerializeField] private Vector3 largeStretchScale = new Vector3(0.8f, 1.3f, 0.8f);

        [Header("Color Pulse (Optional)")]
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Color hoverColor = Color.white * 1.1f;
        [SerializeField] private float colorPulseIntensity = 0.2f;
        [SerializeField] private float colorPulseDuration = 0.25f;

        [Header("Shadow (Optional)")]
        [SerializeField] private Transform shadowTransform;
        [SerializeField] private Vector3 shadowBaseScale = Vector3.one;
        [SerializeField] private float shadowScaleFactor = 0.3f;

        [Header("Audio (Optional)")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip hopTakeoffClip;
        [SerializeField] private AudioClip hopLandingClip;

        [Header("Debug")]
        [SerializeField] private bool drawGizmos = false;

        private Sequence masterSequence;
        private Vector3 baseScale;
        private float baseY;
        private Material instanceMaterial;
        private Color baseColor = Color.white;

        private void Awake()
        {
            baseScale = transform.localScale;
            baseY = transform.position.y;

            if (targetRenderer != null)
            {
                instanceMaterial = targetRenderer.material;
                baseColor = instanceMaterial.color;
            }

            DOTween.Init();
        }

        private void OnEnable()
        {
            BuildSequence();
        }

        private void OnDisable()
        {
            masterSequence?.Kill();
            masterSequence = null;

            transform.localScale = baseScale;
            transform.position = new Vector3(transform.position.x, baseY, transform.position.z);

            if (instanceMaterial != null)
            {
                instanceMaterial.color = baseColor;
            }

            if (shadowTransform != null)
            {
                shadowTransform.localScale = shadowBaseScale;
            }
        }

        private void BuildSequence()
        {
            masterSequence?.Kill();

            masterSequence = DOTween.Sequence();
            masterSequence.Append(DoHop(smallHopHeight, smallSquashScale, smallStretchScale));
            masterSequence.AppendInterval(GetRandomizedDelay());
            masterSequence.Append(DoHop(smallHopHeight, smallSquashScale, smallStretchScale));
            masterSequence.AppendInterval(GetRandomizedDelay());
            masterSequence.Append(DoHop(bigHopHeight, largeSquashScale, largeStretchScale));
            masterSequence.AppendInterval(GetRandomizedDelay() * 2f);
            masterSequence.SetLoops(-1);
        }

        private Sequence DoHop(float height, Vector3 squashScale, Vector3 stretchScale)
        {
            var sequence = DOTween.Sequence();
            var peakY = baseY + height;

            // Squash anticipation
            sequence.Append(transform.DOScale(Vector3.Scale(baseScale, squashScale), 0.08f).SetEase(Ease.InQuad));

            // Stretch and takeoff
            sequence.Append(transform.DOScale(Vector3.Scale(baseScale, stretchScale), 0.12f).SetEase(Ease.OutQuad));

            // Upward motion
            sequence.Join(transform.DOMoveY(peakY, 0.25f).SetEase(Ease.OutSine).OnStart(PlayTakeoff));

            // Color pulse while airborne
            if (instanceMaterial != null)
            {
                var targetColor = hoverColor * (1f + colorPulseIntensity);
                sequence.Join(instanceMaterial.DOColor(targetColor, colorPulseDuration).SetLoops(2, LoopType.Yoyo));
            }

            // Shadow shrink
            if (shadowTransform != null)
            {
                sequence.Join(shadowTransform.DOScale(shadowBaseScale * (1f - height * shadowScaleFactor), 0.25f).SetEase(Ease.OutSine));
            }

            // Fall + stretch blend
            sequence.Append(transform.DOMoveY(baseY, 0.25f).SetEase(Ease.InSine).OnStart(PlayLanding));

            // Landing squash
            sequence.Join(transform.DOScale(Vector3.Scale(baseScale, squashScale), 0.08f).SetEase(Ease.OutBounce));

            // Shadow grow back
            if (shadowTransform != null)
            {
                sequence.Join(shadowTransform.DOScale(shadowBaseScale, 0.1f).SetEase(Ease.OutQuad));
            }

            // Settling back to base scale
            sequence.Append(transform.DOScale(baseScale, 0.1f).SetEase(Ease.OutElastic));

            return sequence;
        }

        private float GetRandomizedDelay()
        {
            if (hopDelayVariance <= 0f)
            {
                return hopDelay;
            }

            return hopDelay + Random.Range(-hopDelayVariance, hopDelayVariance);
        }

        private void PlayTakeoff()
        {
            if (audioSource != null && hopTakeoffClip != null)
            {
                audioSource.PlayOneShot(hopTakeoffClip);
            }
        }

        private void PlayLanding()
        {
            if (audioSource != null && hopLandingClip != null)
            {
                audioSource.PlayOneShot(hopLandingClip);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos)
            {
                return;
            }

            var startY = Application.isPlaying ? baseY : transform.position.y;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, new Vector3(transform.position.x, startY + smallHopHeight, transform.position.z));

            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, new Vector3(transform.position.x, startY + bigHopHeight, transform.position.z));
        }
    }
}
