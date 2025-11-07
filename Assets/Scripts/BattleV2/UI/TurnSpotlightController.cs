using BattleV2.AnimationSystem.Execution.Routers;
using BattleV2.Core;
using UnityEngine;

namespace BattleV2.UI
{
    /// <summary>
    /// Minimal controller that toggles a spotlight GameObject or CanvasGroup when the UI action fires.
    /// Replace with a richer implementation as needed.
    /// </summary>
    public sealed class TurnSpotlightController : MonoBehaviour
    {
        [SerializeField] private GameObject spotlightRoot;
        [SerializeField] private CanvasGroup spotlightCanvas;
        [SerializeField] private bool hideOnStart = true;

        private void Awake()
        {
            if (hideOnStart)
            {
                Hide(null);
            }
        }

        public void Show(CombatantState actor, AnimationEventPayload payload)
        {
            if (spotlightCanvas != null)
            {
                spotlightCanvas.gameObject.SetActive(true);
                spotlightCanvas.alpha = 1f;
            }

            if (spotlightRoot != null)
            {
                spotlightRoot.SetActive(true);
            }
        }

        public void Hide(CombatantState actor)
        {
            if (spotlightCanvas != null)
            {
                spotlightCanvas.alpha = 0f;
                spotlightCanvas.gameObject.SetActive(false);
            }

            if (spotlightRoot != null)
            {
                spotlightRoot.SetActive(false);
            }
        }
    }
}
