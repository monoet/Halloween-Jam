using System;
using UnityEngine;
using UnityEngine.UI;

namespace BattleV2.UI
{
    /// <summary>
    /// Panel para asignar CP. Solo emite el valor seleccionado.
    /// </summary>
    public sealed class CPChargePanel : MonoBehaviour
    {
        [SerializeField] private Slider slider;
        [SerializeField] private int maxCp = 5;
        [SerializeField] private bool visibleByDefault = false;

        public event Action<int> OnChargeCommitted;

        private void Awake()
        {
            gameObject.SetActive(visibleByDefault);
            if (slider != null)
            {
                slider.wholeNumbers = true;
                slider.minValue = 0;
                slider.maxValue = Mathf.Max(0, maxCp);
                slider.onValueChanged.AddListener(OnSliderChanged);
            }
        }

        public void ConfigureMax(int maxValue)
        {
            maxCp = Mathf.Max(0, maxValue);
            if (slider != null)
            {
                slider.maxValue = maxCp;
            }
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void ShowIfAllowed()
        {
            // Placeholder: en producción, ocultar si la política del spell es CP-none.
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void OnSliderChanged(float value)
        {
            OnChargeCommitted?.Invoke(Mathf.RoundToInt(value));
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (BattleV2.Core.BattleDiagnostics.DevCpTrace)
            {
                BattleV2.Core.BattleDiagnostics.Log(
                    "CPTRACE",
                    $"UI_EMIT action=(unknown) amount={Mathf.RoundToInt(value)} source=slider max={maxCp} playerCp=? controlInstance={GetInstanceID()}",
                    this);
            }
#endif
        }
    }
}
