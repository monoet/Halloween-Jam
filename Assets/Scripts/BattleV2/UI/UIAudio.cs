using UnityEngine;
#if FMOD_PRESENT
using FMODUnity;
#endif

namespace BattleV2.UI
{
    /// <summary>
    /// Lightweight helpers for UI SFX (navigation/confirm/back).
    /// </summary>
    public static class UIAudio
    {
#if FMOD_PRESENT
        private const string BackPath = "event:/SFX/combat/ui/Cursor_Back";
#endif

        public static void PlayBack()
        {
#if FMOD_PRESENT
            RuntimeManager.PlayOneShot(BackPath);
#endif
        }
    }
}
