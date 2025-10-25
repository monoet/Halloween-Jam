using System.Collections;
using UnityEngine;

/// <summary>
/// Movimiento por grilla tipo JRPG (Pokémon / Breath of Fire):
/// - Movimiento suave y preciso entre tiles.
/// - Congela animación al terminar el paso (último frame o primero).
/// - Evita teleports usando Rigidbody2D.MovePosition.
/// - No permite diagonales.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class GridMovementV2 : MonoBehaviour
{
    [Header("Grid Settings")]
    [Tooltip("Tamaño de cada tile en unidades de mundo.")]
    [SerializeField] private float tileSize = 1f;

    [Tooltip("Tiempo en segundos para recorrer un tile.")]
    [SerializeField] private float moveTime = 0.15f;

    [Tooltip("Capas que bloquean el paso.")]
    [SerializeField] private LayerMask obstacleMask;

    private bool isMoving;
    private Coroutine moveRoutine;

    private Vector2Int gridPos;
    private Rigidbody2D rb;
    private PlayerAnimatorController anim;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.freezeRotation = true;
    }

    private void Start()
    {
        anim = GetComponent<PlayerAnimatorController>();

        gridPos = WorldToGrid(transform.position);
        rb.position = GridToWorld(gridPos, transform.position.z);
    }

    private void Update()
    {
        if (!isMoving)
            HandleInput();
    }

    private void HandleInput()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");

        // solo un eje activo (sin diagonales)
        if (Mathf.Abs(x) > Mathf.Abs(y)) y = 0;
        else x = 0;

        Vector2Int dir = new Vector2Int(
            x > 0 ? 1 : (x < 0 ? -1 : 0),
            y > 0 ? 1 : (y < 0 ? -1 : 0)
        );

        if (dir == Vector2Int.zero)
            return;

        Vector2Int targetGrid = gridPos + dir;
        Vector3 destination = GridToWorld(targetGrid, transform.position.z);

        if (!IsBlocked(destination))
        {
            if (moveRoutine != null) StopCoroutine(moveRoutine);
            moveRoutine = StartCoroutine(MoveTo(targetGrid, destination, dir));
        }
    }

    private bool IsBlocked(Vector3 destinationWorld)
    {
        const float r = 0.15f;
        Collider2D hit = Physics2D.OverlapCircle(destinationWorld, r, obstacleMask);
        return hit != null;
    }

    private IEnumerator MoveTo(Vector2Int targetGrid, Vector3 destinationWorld, Vector2Int dir)
    {
        isMoving = true;

        if (anim != null)
            anim.PlayMoveAnimation(dir);

        Vector3 start = rb.position;
        float elapsed = 0f;

        while (elapsed < moveTime)
        {
            float t = elapsed / moveTime;
            Vector3 nextPos = Vector3.Lerp(start, destinationWorld, t);
            rb.MovePosition(nextPos);
            elapsed += Time.deltaTime;
            yield return new WaitForFixedUpdate();
        }

        rb.MovePosition(destinationWorld);
        gridPos = targetGrid;

        if (anim != null)
            anim.StopAnimation();

        isMoving = false;
        moveRoutine = null;
    }

    private Vector2Int WorldToGrid(Vector3 world)
    {
        return new Vector2Int(
            Mathf.RoundToInt(world.x / tileSize),
            Mathf.RoundToInt(world.y / tileSize)
        );
    }

    private Vector3 GridToWorld(Vector2Int grid, float z)
    {
        return new Vector3(grid.x * tileSize, grid.y * tileSize, z);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        var w = GridToWorld(gridPos, transform.position.z);
        Gizmos.DrawWireSphere(w, 0.05f);
    }
}
