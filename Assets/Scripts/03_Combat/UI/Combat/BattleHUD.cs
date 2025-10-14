using HalloweenJam.Combat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HalloweenJam.UI.Combat
{
    public class BattleHUD : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private Slider hpSlider;

        private ICombatEntity boundEntity;

        public void Bind(ICombatEntity entity)
        {
            if (boundEntity == entity)
            {
                Refresh();
                return;
            }

            Unbind();
            boundEntity = entity;

            if (boundEntity == null)
            {
                Clear();
                return;
            }

            boundEntity.OnHealthChanged += HandleHealthChanged;
            boundEntity.OnDefeated += HandleHealthChanged;
            Refresh();
        }

        public void Unbind()
        {
            if (boundEntity == null)
            {
                return;
            }

            boundEntity.OnHealthChanged -= HandleHealthChanged;
            boundEntity.OnDefeated -= HandleHealthChanged;
            boundEntity = null;
        }

        private void OnDestroy()
        {
            Unbind();
        }

        private void HandleHealthChanged(ICombatEntity _)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (boundEntity == null)
            {
                Clear();
                return;
            }

            if (nameText != null)
            {
                nameText.text = boundEntity.DisplayName;
            }

            if (hpSlider != null)
            {
                hpSlider.maxValue = boundEntity.MaxHp;
                hpSlider.value = boundEntity.CurrentHp;
            }

            if (hpText != null)
            {
                hpText.text = $"{boundEntity.CurrentHp}/{boundEntity.MaxHp}";
            }
        }

        private void Clear()
        {
            if (nameText != null)
            {
                nameText.text = string.Empty;
            }

            if (hpText != null)
            {
                hpText.text = string.Empty;
            }

            if (hpSlider != null)
            {
                hpSlider.value = 0f;
                hpSlider.maxValue = 1f;
            }
        }
    }
}
