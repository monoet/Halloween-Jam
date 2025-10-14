using UnityEngine;

/// <summary>
/// Mantiene el sprite siempre mirando a la cámara (solo en el eje Y por defecto).
/// </summary>
public class BillboardSprite : MonoBehaviour
{
    [SerializeField] private bool yawOnly = true;

    private void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;

        if (yawOnly)
        {
            Vector3 dir = cam.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(-dir);
        }
        else
        {
            transform.rotation = Quaternion.LookRotation(-cam.transform.forward, cam.transform.up);
        }
    }
}

