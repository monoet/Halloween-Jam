using UnityEngine;

namespace BattleV2.Audio
{
    /// <summary>
    /// Bridge: escucha BattleUIRoot.OnUIModeChanged y reenvía a un parámetro global de FMOD.
    /// </summary>
    public sealed class BattleUIModeFmodBridge : MonoBehaviour
    {
        [SerializeField] private BattleV2.UI.BattleUIRoot uiRoot;
        [SerializeField] private string parameterName = "battle_ui_mode";
        [SerializeField] private bool applyOnEnable = true;

        private void OnEnable()
        {
            if (uiRoot == null)
            {
                return;
            }

            uiRoot.OnUIModeChanged += HandleMode;

            if (applyOnEnable)
            {
                HandleMode(uiRoot.CurrentUIMode);
            }
        }

        private void OnDisable()
        {
            if (uiRoot == null)
            {
                return;
            }

            uiRoot.OnUIModeChanged -= HandleMode;
        }

        private void HandleMode(BattleV2.UI.BattleUIRoot.BattleUIMode mode)
        {
#if FMOD_PRESENT || FMOD
            var result = FMODUnity.RuntimeManager.StudioSystem.setParameterByName(parameterName, (float)mode);
            if (result != FMOD.RESULT.OK)
            {
                Debug.LogWarning($"[BattleUIModeFmodBridge] Failed setParameterByName('{parameterName}', {(float)mode}) => {result}");
            }
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                parameterName = "battle_ui_mode";
            }
        }
#endif
    }
}
