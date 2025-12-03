using BattleV2.Charge;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if FMOD_PRESENT
using FMODUnity;
#endif

namespace BattleV2.UI
{
    public sealed class CpIntentHudText : MonoBehaviour
    {
        [SerializeField] private Text label;
        [SerializeField] private TMP_Text tmpLabel;
        [SerializeField] private bool useSharedInstance = true;
        [SerializeField] private RuntimeCPIntent cpIntentInstance;
#if FMOD_PRESENT
        [Header("Audio (direct)")]
        [SerializeField] private bool playIncreaseSfx = true;
        [SerializeField] private string cpIncreaseEventPath = "event:/SFX/ui/CP_Increase";
        [SerializeField] private bool playDecreaseSfx = true;
        [SerializeField] private string cpDecreaseEventPath = "event:/SFX/ui/CP_Decrease";
#endif

        private ICpIntentSource source;

        private void Awake()
        {
            ResolveSource();
            ApplyVisibility();
        }

        private void OnEnable()
        {
            Subscribe(true);
            ApplyVisibility();
            UpdateLabel();
        }

        private void OnDisable()
        {
            Subscribe(false);
        }

        private void ResolveSource()
        {
            ResolveLabel();

            RuntimeCPIntent runtime = null;
            if (cpIntentInstance != null)
            {
                runtime = cpIntentInstance;
            }
            else if (useSharedInstance)
            {
                runtime = RuntimeCPIntent.Shared;
            }

            source = runtime;
        }

        private void ResolveLabel()
        {
            if (label == null)
            {
                label = GetComponent<Text>();
            }
            if (label == null)
            {
                label = GetComponentInChildren<Text>(true);
            }
            if (tmpLabel == null)
            {
                tmpLabel = GetComponent<TMP_Text>();
            }
            if (tmpLabel == null)
            {
                tmpLabel = GetComponentInChildren<TMP_Text>(true);
            }
        }

        private void Subscribe(bool enable)
        {
            if (source == null)
            {
                return;
            }

            if (enable)
            {
                source.OnChanged += HandleChanged;
                source.OnTurnStarted += HandleTurnStarted;
                source.OnTurnEnded += HandleTurnEnded;
            }
            else
            {
                source.OnChanged -= HandleChanged;
                source.OnTurnStarted -= HandleTurnStarted;
                source.OnTurnEnded -= HandleTurnEnded;
            }
        }

        private void HandleChanged(CpIntentChangedEvent evt)
        {
            UpdateLabel();
#if FMOD_PRESENT
            PlayCpChangeAudio(evt.Previous, evt.Current);
#endif
        }
        private void HandleTurnStarted(CpIntentTurnStartedEvent evt) => ApplyVisibility();
        private void HandleTurnEnded(CpIntentTurnEndedEvent evt) => ApplyVisibility();

        private void UpdateLabel()
        {
            if (source == null)
            {
                return;
            }

            string value = $"CP: {source.Current}/{source.Max}";

            if (tmpLabel != null)
            {
                tmpLabel.text = value;
                return;
            }
            if (label != null)
            {
                label.text = value;
            }
        }

        private void ApplyVisibility()
        {
            if (source == null)
            {
                return;
            }

            bool active = source.IsActiveTurn;
            if (tmpLabel != null)
            {
                tmpLabel.gameObject.SetActive(active);
            }
            if (label != null)
            {
                label.gameObject.SetActive(active);
            }
        }

#if FMOD_PRESENT
        private void PlayCpChangeAudio(int previous, int current)
        {
            int delta = current - previous;
            if (delta == 0)
            {
                return;
            }

            if (delta > 0)
            {
                if (playIncreaseSfx && !string.IsNullOrWhiteSpace(cpIncreaseEventPath))
                {
                    RuntimeManager.PlayOneShot(cpIncreaseEventPath);
                }
            }
            else
            {
                if (playDecreaseSfx && !string.IsNullOrWhiteSpace(cpDecreaseEventPath))
                {
                    RuntimeManager.PlayOneShot(cpDecreaseEventPath);
                }
            }
        }
#endif

    }
}
