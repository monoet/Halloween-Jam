using UnityEngine;

public sealed class CiroDebugFlagListener : MonoBehaviour
{
    private void OnEnable()
    {
        StepSchedulerFlagBus.OnFlag += HandleFlag;
    }

    private void OnDisable()
    {
        StepSchedulerFlagBus.OnFlag -= HandleFlag;
    }

    private void HandleFlag(string flagId)
    {
        Debug.Log($"[CiroFlag] {flagId}");
    }
}
