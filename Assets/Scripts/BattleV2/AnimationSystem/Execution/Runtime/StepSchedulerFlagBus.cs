using System;

public static class StepSchedulerFlagBus
{
    public static event Action<string> OnFlag;

    public static void Emit(string flagId)
    {
        if (!string.IsNullOrWhiteSpace(flagId))
            OnFlag?.Invoke(flagId);
    }
}
