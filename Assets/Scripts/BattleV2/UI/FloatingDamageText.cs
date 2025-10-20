using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace BattleV2.UI
{
    /// <summary>
    /// Simple floating text for damage/healing numbers.
    /// </summary>
    public class FloatingDamageText : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private float lifetime = 1f;
        [SerializeField] private float moveDistance = 1f;
        [SerializeField] private Ease moveEase = Ease.OutQuad;

        private Tween activeTween;

        private void Awake()
        {
            if (label == null)
            {
                label = GetComponentInChildren<TMP_Text>();
            }
        }

        public void Initialise(int amount, bool isHealing, Color? overrideColor = null)
        {
            if (label != null)
            {
                label.text = amount.ToString();
                label.color = overrideColor ?? (isHealing ? Color.green : Color.red);
            }

            if (activeTween != null && activeTween.IsActive())
            {
                activeTween.Kill();
            }

            Vector3 start = transform.position;
            Vector3 end = start + Vector3.up * moveDistance;

            activeTween = transform.DOMove(end, lifetime)
                .SetEase(moveEase)
                .OnComplete(() => Destroy(gameObject));
        }

        private void OnDisable()
        {
            if (activeTween != null)
            {
                activeTween.Kill();
            }
        }
    }
}
