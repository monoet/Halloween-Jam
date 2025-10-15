using UnityEngine;

/// <summary>
/// Cámara fija centrada en el jugador (sin smoothing).
/// Mantiene al target exactamente en el centro de la vista.
/// Ideal para juegos por grilla tipo JRPG clásico.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;

    // Offset en caso de que necesites ajustar el Z o un ligero desplazamiento
    [SerializeField] private Vector3 offset = new Vector3(0, 0, -10);

    // Opcional: límites del mapa (asigna solo si quieres restringir la cámara)
    [SerializeField] private bool useBounds = false;
    [SerializeField] private Vector2 minBounds;
    [SerializeField] private Vector2 maxBounds;

    private Camera cam;
    private float camHalfHeight;
    private float camHalfWidth;

    private void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
            cam = Camera.main;

        if (cam != null)
        {
            camHalfHeight = cam.orthographicSize;
            camHalfWidth = cam.aspect * camHalfHeight;
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 newPos = target.position + offset;

        if (useBounds)
        {
            float clampedX = Mathf.Clamp(newPos.x, minBounds.x + camHalfWidth, maxBounds.x - camHalfWidth);
            float clampedY = Mathf.Clamp(newPos.y, minBounds.y + camHalfHeight, maxBounds.y - camHalfHeight);
            transform.position = new Vector3(clampedX, clampedY, newPos.z);
        }
        else
        {
            transform.position = newPos;
        }
    }
}
