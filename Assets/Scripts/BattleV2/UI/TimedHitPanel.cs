using System;
using UnityEngine;
using UnityEngine.UI;

namespace BattleV2.UI
{
    /// <summary>
    /// Panel simple para disparar input de timed hit.
    /// </summary>
    public sealed class TimedHitPanel : MonoBehaviour
    {
        [SerializeField] private Button hitButton;

        public event Action OnTimedHitPressed;

        private void Awake()
        {
            hitButton?.onClick.AddListener(() => OnTimedHitPressed?.Invoke());
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
