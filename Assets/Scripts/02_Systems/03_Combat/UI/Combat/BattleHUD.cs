using HalloweenJam.Combat;
using HalloweenJam.Combat.Animations;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace HalloweenJam.UI.Combat
{
    public class BattleHUD : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text hpText;
        [SerializeField] private Slider hpSlider;
        [SerializeField] private TMP_Text spText;
        [SerializeField] private Slider spSlider;
        [SerializeField] private TMP_Text cpText;
        [SerializeField] private Slider cpSlider;
        [SerializeField] private BattleHUDFeedback feedback;

        private ICombatEntity boundEntity;
        private RuntimeCombatEntity runtimeEntity;
        private CombatantState combatantState;
        private CharacterRuntime characterRuntime;
        private UnityAction vitalsListener;
        private UnityAction statsListener;

        private int lastHp = -1;
        private int lastSp = -1;
        private int lastCp = -1;

        public void Bind(ICombatEntity entity)
        {
            if (boundEntity == entity)
            {
                Refresh();
                return;
            }

            Unbind();
            boundEntity = entity;
            runtimeEntity = entity as RuntimeCombatEntity;
            combatantState = runtimeEntity != null ? runtimeEntity.CombatantState : null;
            characterRuntime = runtimeEntity != null ? runtimeEntity.CharacterRuntime : null;

            if (boundEntity == null)
            {
                Clear();
                return;
            }

            boundEntity.OnHealthChanged += HandleHealthChanged;
            boundEntity.OnDefeated += HandleHealthChanged;

            if (combatantState != null)
            {
                vitalsListener ??= HandleVitalsChangedUnity;
                combatantState.OnVitalsChanged.AddListener(vitalsListener);
            }

            if (characterRuntime != null)
            {
                statsListener ??= HandleStatsChangedUnity;
                characterRuntime.OnStatsChanged.AddListener(statsListener);
            }

            lastHp = lastSp = lastCp = -1;
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

            if (combatantState != null && vitalsListener != null)
            {
                combatantState.OnVitalsChanged.RemoveListener(vitalsListener);
            }

            if (characterRuntime != null && statsListener != null)
            {
                characterRuntime.OnStatsChanged.RemoveListener(statsListener);
            }

            runtimeEntity = null;
            combatantState = null;
            characterRuntime = null;
            lastHp = lastSp = lastCp = -1;
        }

        private void OnDestroy()
        {
            Unbind();
        }

        public void ForceRefresh()
        {
            Refresh();
        }

        public void PlayPhaseCue(AttackAnimationPhase phase, bool isActor)
        {
            if (feedback == null)
            {
                return;
            }

            switch (phase)
            {
                case AttackAnimationPhase.Charge:
                    if (isActor)
                    {
                        feedback.PlayChargeCue();
                    }
                    break;
                case AttackAnimationPhase.Lunge:
                    if (isActor)
                    {
                        feedback.PlayLungeCue();
                    }
                    break;
                case AttackAnimationPhase.Impact:
                    if (isActor)
                    {
                        feedback.PlayImpactCue();
                    }
                    else
                    {
                        feedback.PlayIncomingDamageCue();
                    }
                    break;
                case AttackAnimationPhase.Recover:
                    if (isActor)
                    {
                        feedback.PlayRecoverCue();
                    }
                    break;
            }
        }

        private void HandleHealthChanged(ICombatEntity _)
        {
            Refresh();
        }

        private void HandleVitalsChangedUnity()
        {
            Refresh();
        }

        private void HandleStatsChangedUnity()
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

            int currentHp = combatantState != null ? combatantState.CurrentHP : boundEntity.CurrentHp;
            int maxHp = combatantState != null ? combatantState.MaxHP : boundEntity.MaxHp;
            int currentSp = combatantState != null ? combatantState.CurrentSP : 0;
            int maxSp = combatantState != null ? combatantState.MaxSP : 0;
            int currentCp = combatantState != null ? combatantState.CurrentCP : 0;
            int maxCp = combatantState != null ? combatantState.MaxCP : 0;

            if (nameText != null)
            {
                nameText.text = boundEntity.DisplayName;
            }

            if (hpSlider != null)
            {
                hpSlider.maxValue = maxHp;
                hpSlider.value = currentHp;
            }

            if (hpText != null)
            {
                hpText.text = $"{currentHp}/{maxHp}";
            }

            if (spSlider != null)
            {
                spSlider.maxValue = maxSp > 0 ? maxSp : 1f;
                spSlider.value = Mathf.Clamp(currentSp, 0, spSlider.maxValue);
            }

            if (spText != null)
            {
                spText.text = maxSp > 0 ? $"{currentSp}/{maxSp}" : string.Empty;
            }

            if (cpSlider != null)
            {
                cpSlider.maxValue = maxCp > 0 ? maxCp : 1f;
                cpSlider.value = Mathf.Clamp(currentCp, 0, cpSlider.maxValue);
            }

            if (cpText != null)
            {
                cpText.text = maxCp > 0 ? $"{currentCp}/{maxCp}" : string.Empty;
            }

            TriggerFeedback(currentHp, currentSp, currentCp);

            lastHp = currentHp;
            lastSp = currentSp;
            lastCp = currentCp;
        }

        private void TriggerFeedback(int currentHp, int currentSp, int currentCp)
        {
            if (feedback == null)
            {
                return;
            }

            if (lastHp >= 0)
            {
                int deltaHp = currentHp - lastHp;
                if (deltaHp < 0)
                {
                    feedback.PlayDamage(-deltaHp);
                }
                else if (deltaHp > 0)
                {
                    feedback.PlayHeal(deltaHp);
                }
            }

            if (lastCp >= 0)
            {
                int deltaCp = currentCp - lastCp;
                if (deltaCp != 0)
                {
                    feedback.PlayCpChange(deltaCp);
                }
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

            if (spText != null)
            {
                spText.text = string.Empty;
            }

            if (spSlider != null)
            {
                spSlider.value = 0f;
                spSlider.maxValue = 1f;
            }

            if (cpText != null)
            {
                cpText.text = string.Empty;
            }

            if (cpSlider != null)
            {
                cpSlider.value = 0f;
                cpSlider.maxValue = 1f;
            }

            lastHp = lastSp = lastCp = -1;
        }
    }
}
