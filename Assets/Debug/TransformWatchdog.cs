using UnityEngine;

public sealed class TransformWatchdog : MonoBehaviour
{
    public bool enableWatch = false;
    private Vector3 lastPos;

    private void Awake()
    {
        lastPos = transform.localPosition;
    }

    private void LateUpdate()
    {
        if (!enableWatch)
        {
            return;
        }

        if (transform.localPosition != lastPos)
        {
            Debug.LogError($"[WATCHDOG] WRITE on {name}: {transform.localPosition}\n" + StackTraceUtility.ExtractStackTrace());
            lastPos = transform.localPosition;
        }
    }
}
