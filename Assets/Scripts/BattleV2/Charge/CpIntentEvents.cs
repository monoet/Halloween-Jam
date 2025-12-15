namespace BattleV2.Charge
{
    public readonly struct CpIntentChangedEvent
    {
        public CpIntentChangedEvent(int previous, int current, int max, string reason)
        {
            Previous = previous;
            Current = current;
            Max = max;
            Reason = reason;
        }

        public int Previous { get; }
        public int Current { get; }
        public int Max { get; }
        public string Reason { get; }
    }

    public readonly struct CpIntentConsumedEvent
    {
        public CpIntentConsumedEvent(int amount, int remaining, string reason, int selectionId)
        {
            Amount = amount;
            Remaining = remaining;
            Reason = reason;
            SelectionId = selectionId;
        }

        public int Amount { get; }
        public int Remaining { get; }
        public string Reason { get; }
        public int SelectionId { get; }
    }

    public readonly struct CpIntentCanceledEvent
    {
        public CpIntentCanceledEvent(int previous, string reason)
        {
            Previous = previous;
            Reason = reason;
        }

        public int Previous { get; }
        public string Reason { get; }
    }

    public readonly struct CpIntentTurnStartedEvent
    {
        public CpIntentTurnStartedEvent(int max)
        {
            Max = max;
        }

        public int Max { get; }
    }

    public readonly struct CpIntentTurnEndedEvent
    {
        public CpIntentTurnEndedEvent(int finalValue, string reason)
        {
            FinalValue = finalValue;
            Reason = reason;
        }

        public int FinalValue { get; }
        public string Reason { get; }
    }
}
