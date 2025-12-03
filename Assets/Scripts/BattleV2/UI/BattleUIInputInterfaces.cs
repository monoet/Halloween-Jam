namespace BattleV2.UI
{
    public interface IBattleUIInput
    {
        bool ConfirmHeld { get; }
        bool ConfirmPressedThisFrame { get; }
        bool CancelHeld { get; }
        bool CancelPressedThisFrame { get; }
        UnityEngine.Vector2 NavigationDirection { get; }
        bool TimedHitPressedThisFrame { get; }
    }

    public interface IInputGate
    {
        void OnEnter(IBattleUIInput input);
        bool AllowConfirm(IBattleUIInput input);
        bool AllowCancel(IBattleUIInput input);
    }

    public sealed class NoGate : IInputGate
    {
        public void OnEnter(IBattleUIInput input) { }
        public bool AllowConfirm(IBattleUIInput input) => input.ConfirmPressedThisFrame;
        public bool AllowCancel(IBattleUIInput input) => input.CancelPressedThisFrame;
    }

    public sealed class WaitReleaseThenPressGate : IInputGate
    {
        private bool waitingRelease;

        public void OnEnter(IBattleUIInput input)
        {
            waitingRelease = input.ConfirmHeld;
        }

        public bool AllowConfirm(IBattleUIInput input)
        {
            if (waitingRelease)
            {
                if (!input.ConfirmHeld)
                {
                    waitingRelease = false;
                    UnityEngine.Debug.Log("[WaitReleaseGate] Key released. Ready for press.");
                }
                else
                {
                    // UnityEngine.Debug.Log("[WaitReleaseGate] Waiting for release...");
                }
                return false;
            }
            
            bool pressed = input.ConfirmPressedThisFrame;
            if (pressed)
            {
                 UnityEngine.Debug.Log("[WaitReleaseGate] Confirm Allowed!");
            }
            return pressed;
        }

        public bool AllowCancel(IBattleUIInput input)
        {
            bool pressed = input.CancelPressedThisFrame;
            if (pressed) UnityEngine.Debug.Log("[WaitReleaseGate] Cancel Pressed!");
            return pressed;
        }
    }
}
