namespace BattleV2.UI.ActionMenu
{
    public readonly struct ActionMenuInput
    {
        public ActionMenuInput(int vertical, int horizontal, bool confirm, bool cancel, bool charge, bool leftBumper, bool rightBumper)
        {
            Vertical = vertical;
            Horizontal = horizontal;
            ConfirmPressed = confirm;
            CancelPressed = cancel;
            ChargeHeld = charge;
            LeftBumper = leftBumper;
            RightBumper = rightBumper;
        }

        public int Vertical { get; }
        public int Horizontal { get; }
        public bool ConfirmPressed { get; }
        public bool CancelPressed { get; }
        public bool ChargeHeld { get; }
        public bool LeftBumper { get; }
        public bool RightBumper { get; }

        public static ActionMenuInput Empty => new ActionMenuInput(0, 0, false, false, false, false, false);
    }
}
