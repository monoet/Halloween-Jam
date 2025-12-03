using UnityEngine;
using UnityEngine.EventSystems;
#if FMOD_PRESENT
using FMODUnity;
#endif

namespace BattleV2.UI
{
    /// <summary>
    /// Reproduce SFX de navegación (move/submit/cancel) a nivel global.
    /// Colócalo en el root de UI (junto a BattleUIRoot) con un EventSystem presente.
    /// </summary>
    public sealed class UINavigationSfx : MonoBehaviour, IMoveHandler, ISubmitHandler, ICancelHandler
    {
        [Header("Enable")]
        [SerializeField] private bool playMove = true;
        [SerializeField] private bool playSubmit = true;
        [SerializeField] private bool playCancel = true;

#if FMOD_PRESENT
        [Header("FMOD Event Paths")]
        [SerializeField] private string moveEventPath = "event:/SFX/combat/ui/Cursor_Movement";
        [SerializeField] private string submitEventPath = "event:/SFX/combat/ui/Cursor_Confirm";
        [SerializeField] private string cancelEventPath = "event:/SFX/combat/ui/Cursor_Back";
#endif

        public void OnMove(AxisEventData eventData)
        {
#if FMOD_PRESENT
            if (!playMove) return;
            if (eventData == null || eventData.moveVector.sqrMagnitude <= 0.01f) return;
            if (!string.IsNullOrWhiteSpace(moveEventPath))
            {
                RuntimeManager.PlayOneShot(moveEventPath);
            }
#endif
        }

        public void OnSubmit(BaseEventData eventData)
        {
#if FMOD_PRESENT
            if (!playSubmit) return;
            if (!string.IsNullOrWhiteSpace(submitEventPath))
            {
                RuntimeManager.PlayOneShot(submitEventPath);
            }
#endif
        }

        public void OnCancel(BaseEventData eventData)
        {
#if FMOD_PRESENT
            if (!playCancel) return;
            if (!string.IsNullOrWhiteSpace(cancelEventPath))
            {
                RuntimeManager.PlayOneShot(cancelEventPath);
            }
#endif
        }
    }
}
