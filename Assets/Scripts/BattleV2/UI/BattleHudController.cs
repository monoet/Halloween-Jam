using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BattleV2.UI
{
    /// <summary>
    /// Updates HUD elements for a combatant.
    /// </summary>
    public class BattleHUDController : MonoBehaviour
    {
        [SerializeField] private CombatantState state;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text hpLabel;
        [SerializeField] private TMP_Text spLabel;
        [SerializeField] private TMP_Text cpLabel;
        [SerializeField] private Slider hpSlider;
        [SerializeField] private Slider spSlider;
        [SerializeField] private Slider cpSlider;

        private void OnEnable()
        {
            if (state != null)
            {
                state.OnVitalsChanged.AddListener(HandleVitalsChanged);
                RefreshFromState(state);
            }
        }

        private void OnDisable()
        {
            if (state != null)
            {
                state.OnVitalsChanged.RemoveListener(HandleVitalsChanged);
            }
        }

        public void SetState(CombatantState newState)
        {
            if (state == newState)
            {
                return;
            }

            if (state != null)
            {
                state.OnVitalsChanged.RemoveListener(HandleVitalsChanged);
            }

            state = newState;

            if (state != null)
            {
                state.OnVitalsChanged.AddListener(HandleVitalsChanged);
                RefreshFromState(state);
            }
            else
            {
                Clear();
            }
        }

        public void RefreshFromState(CombatantState target)
        {
            if (target == null)
            {
                Clear();
                return;
            }

            if (nameLabel != null)
            {
                nameLabel.text = target.name;
            }

            if (hpLabel != null)
            {
                hpLabel.text = $"{target.CurrentHP}/{target.MaxHP}";
            }

            if (spLabel != null)
            {
                spLabel.text = $"{target.CurrentSP}/{target.MaxSP}";
            }

            if (cpLabel != null)
            {
                cpLabel.text = $"{target.CurrentCP}/{target.MaxCP}";
            }

            if (hpSlider != null)
            {
                hpSlider.value = SafeRatio(target.CurrentHP, target.MaxHP);
            }

            if (spSlider != null)
            {
                spSlider.value = SafeRatio(target.CurrentSP, target.MaxSP);
            }

            if (cpSlider != null)
            {
                cpSlider.value = SafeRatio(target.CurrentCP, target.MaxCP);
            }
        }

        private void Clear()
        {
            if (nameLabel != null)
            {
                nameLabel.text = "--";
            }

            if (hpLabel != null)
            {
                hpLabel.text = "--/--";
            }

            if (spLabel != null)
            {
                spLabel.text = "--/--";
            }

            if (cpLabel != null)
            {
                cpLabel.text = "--/--";
            }

            if (hpSlider != null)
            {
                hpSlider.value = 0f;
            }

            if (spSlider != null)
            {
                spSlider.value = 0f;
            }

            if (cpSlider != null)
            {
                cpSlider.value = 0f;
            }
        }

        private void HandleVitalsChanged()
        {
            RefreshFromState(state);
        }

        private static float SafeRatio(int current, int max)
        {
            if (max <= 0)
            {
                return 0f;
            }

            return Mathf.Clamp01(current / (float)max);
        }
    }
}
