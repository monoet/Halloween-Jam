using UnityEngine;
using System.Collections;

/// <summary>
/// Movimiento clásico tipo Pokémon / Breath of Fire con control total del Animator por clip name.
/// </summary>
[RequireComponent(typeof(SpriteRenderer), typeof(Animator))]
public class GridMovementV3 : MonoBehaviour
{
    [Header("Grid movement")]
    [SerializeField] private float gridSize = 1f;
    [SerializeField] private float moveDuration = 0.15f;

    private bool isMoving;
    private Vector2 moveDir;
    private Vector2 targetPosition;
    private Vector2 lastMoveDir = Vector2.down;

    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private string currentState;

    [Header("Animator States")]
    [SerializeField] private string idleUp = "IdleUp";
    [SerializeField] private string idleDown = "IdleDown";
    [SerializeField] private string idleSide = "IdleSide";
    [SerializeField] private string walkUp = "WalkUp";
    [SerializeField] private string walkDown = "WalkDown";
    [SerializeField] private string walkSide = "WalkSide";

    [Header("Intro Settings")]
    [SerializeField] private float introDelay = 1f;
    [SerializeField] private float introDuration = 2f;
    private bool introPlaying = true;

    private void Start()
    {
        targetPosition = SnapToGrid(transform.position);
        transform.position = targetPosition;

        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        ChangeAnimationState(idleDown);

        if (introDuration > 0)
            StartCoroutine(PlayIntro());
        else
            introPlaying = false;
    }

    private void Update()
    {
        if (introPlaying) return;
        if (isMoving) return;

        moveDir = Vector2.zero;

        // Input WASD
        if (Input.GetKey(KeyCode.W)) moveDir = Vector2.up;
        else if (Input.GetKey(KeyCode.S)) moveDir = Vector2.down;
        else if (Input.GetKey(KeyCode.A)) moveDir = Vector2.left;
        else if (Input.GetKey(KeyCode.D)) moveDir = Vector2.right;

        if (moveDir != Vector2.zero)
        {
            targetPosition += moveDir * gridSize;
            StartCoroutine(MoveToCell(targetPosition));
            PlayWalkAnim(moveDir);
            lastMoveDir = moveDir;
        }
        else
        {
            PlayIdleAnim();
        }
    }

    private IEnumerator MoveToCell(Vector2 destination)
    {
        isMoving = true;

        Vector2 start = transform.position;
        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / moveDuration);
            transform.position = Vector2.Lerp(start, destination, t);
            yield return null;
        }

        transform.position = destination;
        isMoving = false;
    }

    private IEnumerator PlayIntro()
    {
        yield return new WaitForSeconds(introDelay);

        float elapsed = 0f;
        while (elapsed < introDuration)
        {
            targetPosition += Vector2.up * gridSize;
            StartCoroutine(MoveToCell(targetPosition));
            PlayWalkAnim(Vector2.up);
            lastMoveDir = Vector2.up;

            elapsed += moveDuration;
            yield return new WaitForSeconds(moveDuration);
        }

        introPlaying = false;
        PlayIdleAnim();
    }

    private void PlayWalkAnim(Vector2 dir)
    {
        if (dir == Vector2.up)
        {
            ChangeAnimationState(walkUp);
        }
        else if (dir == Vector2.down)
        {
            ChangeAnimationState(walkDown);
        }
        else
        {
            spriteRenderer.flipX = (dir == Vector2.left);
            ChangeAnimationState(walkSide);
        }
    }

    private void PlayIdleAnim()
    {
        if (lastMoveDir == Vector2.up)
        {
            ChangeAnimationState(idleUp);
        }
        else if (lastMoveDir == Vector2.down)
        {
            ChangeAnimationState(idleDown);
        }
        else
        {
            spriteRenderer.flipX = (lastMoveDir == Vector2.left);
            ChangeAnimationState(idleSide);
        }
    }

    private void ChangeAnimationState(string newState)
    {
        if (animator == null || string.IsNullOrEmpty(newState)) return;
        if (currentState == newState) return;

        animator.Play(newState, 0, 0f);
        currentState = newState;
    }

    private Vector2 SnapToGrid(Vector2 pos)
    {
        float snappedX = Mathf.Round(pos.x / gridSize) * gridSize;
        float snappedY = Mathf.Round(pos.y / gridSize) * gridSize;
        return new Vector2(snappedX, snappedY);
    }
}
