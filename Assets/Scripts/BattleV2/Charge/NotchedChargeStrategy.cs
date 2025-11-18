using System;
using BattleV2.Actions;
using BattleV2.Execution.TimedHits;
using BattleV2.Providers;
using UnityEngine;

namespace BattleV2.Charge
{
    public sealed class NotchedChargeStrategy : IChargeStrategy
    {
        public struct KeyBindings
        {
            public KeyCode Increase;
            public KeyCode Decrease;
            public KeyCode Confirm;
            public KeyCode Cancel;
        }

        private readonly KeyBindings keyBindings;
        private readonly bool listenKeyboard;
        private readonly Action<string> logger;
        private readonly Action<int, int> onChargeChanged;

        private ChargeRequest request;
        private Action<BattleSelection> onCompleted;
        private Action onCancelled;
        private bool active;
        private int currentCharge;
        private int maxCharge;

        public NotchedChargeStrategy(KeyBindings bindings, bool listenKeyboardInput, Action<string> logger = null, Action<int, int> onChargeChanged = null)
        {
            keyBindings = bindings;
            listenKeyboard = listenKeyboardInput;
            this.logger = logger;
            this.onChargeChanged = onChargeChanged;
        }

        public int CurrentCharge => currentCharge;
        public int MaxCharge => maxCharge;

        public void Begin(ChargeRequest request, Action<BattleSelection> onCompleted, Action onCancelled)
        {
            this.request = request;
            this.onCompleted = onCompleted;
            this.onCancelled = onCancelled;
            maxCharge = Mathf.Max(0, request.MaxCharge);
            currentCharge = 0;
            active = true;

            NotifyChargeChanged();

            if (maxCharge <= 0)
            {
                Complete(request.Action, 0);
            }
            else
            {
                Log($"Charge ready. Max CP charge: {maxCharge}. Use increase/decrease inputs, confirm to execute.");
            }
        }

        public void Tick(float deltaTime)
        {
            if (!active || !listenKeyboard)
                return;

            if (keyBindings.Increase != KeyCode.None && Input.GetKeyDown(keyBindings.Increase))
            {
                AdjustCharge(1);
            }

            if (keyBindings.Decrease != KeyCode.None && Input.GetKeyDown(keyBindings.Decrease))
            {
                AdjustCharge(-1);
            }

            if (keyBindings.Confirm != KeyCode.None && Input.GetKeyDown(keyBindings.Confirm))
            {
                Confirm();
            }

            if (keyBindings.Cancel != KeyCode.None && Input.GetKeyDown(keyBindings.Cancel))
            {
                Cancel();
            }
        }

        public void Cancel()
        {
            if (!active)
                return;

            active = false;
            onCancelled?.Invoke();
        }

        public void AdjustCharge(int delta)
        {
            if (!active)
                return;

            if (maxCharge <= 0)
                return;

            int newCharge = Mathf.Clamp(currentCharge + delta, 0, maxCharge);
            if (newCharge == currentCharge)
                return;

            currentCharge = newCharge;
            NotifyChargeChanged();
        }

        public void Confirm()
        {
            if (!active)
                return;

            Complete(request.Action, currentCharge);
        }

        private void Complete(BattleActionData action, int charge)
        {
            if (!active)
                return;

            active = false;
            onCompleted?.Invoke(new BattleSelection(
                action,
                charge,
                request.ChargeProfile,
                request.TimedHitProfile,
                basicTimedHitProfile: request.BasicTimedHitProfile,
                runnerKind: request.RunnerKind));
        }

        private void Log(string message)
        {
            logger?.Invoke(message);
        }

        private void NotifyChargeChanged()
        {
            onChargeChanged?.Invoke(currentCharge, maxCharge);
            Log($"CP Charge: {currentCharge}/{maxCharge}");
        }
    }
}
