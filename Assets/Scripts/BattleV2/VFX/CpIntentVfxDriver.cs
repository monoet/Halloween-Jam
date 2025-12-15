using BattleV2.Charge;
using UnityEngine;

namespace BattleV2.VFX
{
    public sealed class CpIntentVfxDriver : MonoBehaviour
    {
        [SerializeField] private ParticleSystem aura;
        [SerializeField] private bool useSharedInstance = true;
        [SerializeField] private RuntimeCPIntent cpIntentInstance;

        private ICpIntentSource source;

        private void Awake()
        {
            RuntimeCPIntent runtime = cpIntentInstance != null ? cpIntentInstance : (useSharedInstance ? RuntimeCPIntent.Shared : null);
            source = runtime;
        }

        private void OnEnable()
        {
            Subscribe(true);
        }

        private void OnDisable()
        {
            Subscribe(false);
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
                source.OnConsumed += HandleConsumed;
                source.OnCanceled += HandleCanceled;
                source.OnTurnEnded += HandleTurnEnded;
            }
            else
            {
                source.OnChanged -= HandleChanged;
                source.OnConsumed -= HandleConsumed;
                source.OnCanceled -= HandleCanceled;
                source.OnTurnEnded -= HandleTurnEnded;
            }
        }

        private void HandleChanged(CpIntentChangedEvent evt)
        {
            if (aura == null)
            {
                return;
            }

            var main = aura.main;
            float intensity = evt.Max > 0 ? Mathf.Clamp01((float)evt.Current / evt.Max) : 0f;
            main.startColor = new Color(0.2f, 1f, 0.2f, Mathf.Lerp(0.1f, 0.8f, intensity));
        }

        private void HandleConsumed(CpIntentConsumedEvent evt)
        {
            if (aura != null)
            {
                aura.Play();
            }
        }

        private void HandleCanceled(CpIntentCanceledEvent evt)
        {
            if (aura != null)
            {
                aura.Stop();
            }
        }

        private void HandleTurnEnded(CpIntentTurnEndedEvent evt)
        {
            if (aura != null)
            {
                aura.Stop();
            }
        }
    }
}
