using UnityEngine;
using UnityEngine.UI;
#if FMOD_PRESENT
using FMODUnity;
#endif

namespace BattleV2.UI
{
    /// <summary>
    /// Simple SFX trigger for UI buttons (confirm/cancel click).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class UIButtonSfx : MonoBehaviour
    {
        private enum Preset
        {
            Custom,
            Confirm,
            Back
        }

        [SerializeField] private bool playOnClick = true;
        [SerializeField] private Preset preset = Preset.Custom;
#if FMOD_PRESENT
        [SerializeField] private string eventPath;

        private const string ConfirmDefault = "event:/SFX/combat/ui/Cursor_Confirm";
        private const string BackDefault = "event:/SFX/combat/ui/Cursor_Back";
#endif

        private void Awake()
        {
            if (playOnClick)
            {
                GetComponent<Button>()?.onClick.AddListener(Play);
            }
        }

        public void Play()
        {
#if FMOD_PRESENT
            if (string.IsNullOrWhiteSpace(eventPath))
            {
                eventPath = preset switch
                {
                    Preset.Confirm => ConfirmDefault,
                    Preset.Back => BackDefault,
                    _ => eventPath
                };
            }

            if (!string.IsNullOrWhiteSpace(eventPath))
            {
                RuntimeManager.PlayOneShot(eventPath);
            }
#endif
        }
    }
}
