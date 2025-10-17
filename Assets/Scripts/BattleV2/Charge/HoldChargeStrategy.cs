using System;
using BattleV2.Providers;
using UnityEngine;

namespace BattleV2.Charge
{
    /// <summary>
    /// Implements a press-and-hold mechanic that converts hold duration into CP charge.
    /// </summary>
    public sealed class HoldChargeStrategy : IChargeStrategy
    {
        [SerializeField] private KeyCode holdKey = KeyCode.R;

        private ChargeRequest request;
        private Action<BattleSelection> onCompleted;
        private Action onCancelled;
        private bool charging;
        private float elapsed;
        private int cachedCharge;

        public void Begin(ChargeRequest request, Action<BattleSelection> onCompleted, Action onCancelled)
        {
            this.request = request;
            this.onCompleted = onCompleted;
            this.onCancelled = onCancelled;
            charging = false;
            elapsed = 0f;
            cachedCharge = 0;

            BattleLogger.Log("HoldCharge", $"Hold {holdKey} to charge up to {request.MaxCharge} CP.");
        }

        public void Tick(float deltaTime)
        {
            if (request.MaxCharge <= 0)
            {
                Complete(0);
                return;
            }

            if (Input.GetKeyDown(holdKey))
            {
                charging = true;
                elapsed = 0f;
                cachedCharge = 0;
                BattleLogger.Log("HoldCharge", "Charging started.");
            }

            if (charging)
            {
                elapsed += deltaTime;
                cachedCharge = Mathf.Clamp(Mathf.FloorToInt((elapsed / Mathf.Max(0.0001f, request.Profile?.MaxChargeTime ?? 1f)) * request.MaxCharge), 0, request.MaxCharge);
            }

            if (charging && Input.GetKeyUp(holdKey))
            {
                charging = false;
                Complete(cachedCharge);
            }
        }

        public void Cancel()
        {
            charging = false;
            elapsed = 0f;
            cachedCharge = 0;
            onCancelled?.Invoke();
        }

        private void Complete(int cpCharge)
        {
            onCompleted?.Invoke(new BattleSelection(request.Action, cpCharge, request.Profile));
        }
    }
}
