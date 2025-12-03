using System;

namespace BattleV2.Charge
{
    public interface ICpIntentSource
    {
        int Current { get; }
        int Max { get; }
        bool IsActiveTurn { get; }

        event Action<CpIntentChangedEvent> OnChanged;
        event Action<CpIntentConsumedEvent> OnConsumed;
        event Action<CpIntentCanceledEvent> OnCanceled;
        event Action<CpIntentTurnStartedEvent> OnTurnStarted;
        event Action<CpIntentTurnEndedEvent> OnTurnEnded;
    }
}
