using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class GridMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveTime = 0.15f;   // Tiempo entre tiles
    [SerializeField] private LayerMask obstacleMask;   // Qué bloquea el paso

    private bool isMoving;
    private Vector2 input;
    private Vector3 startPos;
    private Vector3 targetPos;
    private PlayerAnimator anim;

    private void Awake()
    {
        anim = GetComponent<PlayerAnimator>();
        Debug.Log($"[GridMovement] Awake - Component initialized. Animator found: {anim != null}");
        anim = GetComponent<PlayerAnimator>();
       
        if (anim == null)
        {
            foreach (var comp in GetComponents<Component>())
                Debug.Log($"[GridMovement] Found component: {comp.GetType().Name}");
        }
        else
        {
            Debug.Log($"[GridMovement] Animator properly linked: {anim}");
        }

    }
    
   
    private void Start()
    {
        anim = GetComponent<PlayerAnimator>();
        Debug.Log($"[GridMovement] Start - Animator found: {(anim != null)}");
    }



    private void Update()
    {
        if (!isMoving)
            HandleInput();
    }

    private void HandleInput()
    {
        input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        
        if (input != Vector2.zero)
        {
            Debug.Log($"[GridMovement] Raw Input detected: {input}");
        }

        // Previene movimiento diagonal
        if (Mathf.Abs(input.x) > Mathf.Abs(input.y))
            input.y = 0;
        else
            input.x = 0;

        if (input != Vector2.zero)
        {
            Debug.Log($"[GridMovement] Processed Input: {input}");
            var targetPosition = transform.position + new Vector3(input.x, input.y, 0);
            Debug.Log($"[GridMovement] Current Pos: {transform.position}, Target Pos: {targetPosition}");
            
            bool walkable = IsWalkable(targetPosition);
            Debug.Log($"[GridMovement] IsWalkable({targetPosition}): {walkable} (ObstacleMask: {obstacleMask.value})");
            
            if (walkable)
            {
                Debug.Log($"[GridMovement] Starting movement to {targetPosition}");
                StartCoroutine(Move(targetPosition));
            }
            else
            {
                Debug.LogWarning($"[GridMovement] Path blocked at {targetPosition}");
            }
        }
    }

    private bool IsWalkable(Vector3 target)
    {
        Collider2D hit = Physics2D.OverlapCircle(target, 0.1f, obstacleMask);
        if (hit != null)
        {
            Debug.LogWarning($"[GridMovement] Obstacle detected at {target}: {hit.gameObject.name} (Layer: {LayerMask.LayerToName(hit.gameObject.layer)})");
        }
        return hit == null;
    }

    private IEnumerator Move(Vector3 destination)
    {
        Debug.Log($"[GridMovement] Move coroutine started. isMoving: {isMoving} -> true");
        isMoving = true;
        startPos = transform.position;
        targetPos = destination;

        Vector2 dir = (targetPos - startPos).normalized;
        Debug.Log($"[GridMovement] Movement direction: {dir}");
        
        if (anim != null)
        {
            anim.PlayMoveAnimation(dir);
            Debug.Log($"[GridMovement] PlayMoveAnimation called with direction: {dir}");
        }
        else
        {
            Debug.LogError("[GridMovement] PlayerAnimator is NULL!");
        }

        float elapsed = 0f;

        while (elapsed < moveTime)
        {
            transform.position = Vector3.Lerp(startPos, targetPos, elapsed / moveTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPos;
        Debug.Log($"[GridMovement] Move completed. Final position: {targetPos}");
        isMoving = false;
        
        if (anim != null)
        {
            anim.StopAnimation();
            Debug.Log($"[GridMovement] StopAnimation called");
        }
    }
}
