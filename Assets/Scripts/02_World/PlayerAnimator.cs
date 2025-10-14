using System.Collections;
using UnityEngine;

/// <summary>
/// Controla las animaciones direccionales del jugador sin usar Animator Controller.
/// Cambia los sprites según la dirección del movimiento y congela el último frame al detenerse.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerAnimator : MonoBehaviour
{
    [Header("Directional Sprite Sets")]
    [Tooltip("Frames de caminata hacia arriba.")]
    public Sprite[] upSprites;
    [Tooltip("Frames de caminata hacia abajo.")]
    public Sprite[] downSprites;
    [Tooltip("Frames de caminata hacia la izquierda.")]
    public Sprite[] leftSprites;
    [Tooltip("Frames de caminata hacia la derecha.")]
    public Sprite[] rightSprites;

    [Header("Animation Settings")]
    [SerializeField] private float frameRate = 0.12f;

    private SpriteRenderer sr;
    private Coroutine currentAnim;
    private Vector2 lastDir = Vector2.down;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        Debug.Log($"[PlayerAnimator] Awake - SpriteRenderer found: {sr != null}");
        Debug.Log($"[PlayerAnimator] Sprite Sets - Up: {upSprites?.Length ?? 0}, Down: {downSprites?.Length ?? 0}, Left: {leftSprites?.Length ?? 0}, Right: {rightSprites?.Length ?? 0}");
        
        if (sr != null && sr.sprite != null)
        {
            Debug.Log($"[PlayerAnimator] Initial sprite: {sr.sprite.name}");
        }
        else if (sr != null)
        {
            Debug.LogWarning("[PlayerAnimator] SpriteRenderer has no initial sprite assigned!");
        }
    }

    /// <summary>
    /// Llama este método cada vez que el jugador se mueve.
    /// </summary>
    public void PlayMoveAnimation(Vector2 dir)
    {
        Debug.Log($"[PlayerAnimator] PlayMoveAnimation called with direction: {dir}");
        
        if (dir != Vector2.zero)
        {
            lastDir = dir;
            Debug.Log($"[PlayerAnimator] Updated lastDir to: {lastDir}");
        }

        if (currentAnim != null)
        {
            Debug.Log("[PlayerAnimator] Stopping previous animation");
            StopCoroutine(currentAnim);
        }

        Debug.Log($"[PlayerAnimator] Starting AnimateDirection coroutine");
        currentAnim = StartCoroutine(AnimateDirection(dir));
    }

    /// <summary>
    /// Llama este método cuando el jugador se detiene.
    /// </summary>
    public void StopAnimation()
    {
        Debug.Log($"[PlayerAnimator] StopAnimation called. LastDir: {lastDir}");
        
        if (currentAnim != null)
        {
            Debug.Log("[PlayerAnimator] Stopping current animation coroutine");
            StopCoroutine(currentAnim);
            currentAnim = null;
        }

        Sprite[] set = GetSpriteSet(lastDir);
        Debug.Log($"[PlayerAnimator] Selected sprite set length: {set?.Length ?? 0}");
        
        if (set != null && set.Length > 0)
        {
            sr.sprite = set[set.Length - 1]; // congela último frame
            Debug.Log($"[PlayerAnimator] Set idle sprite: {sr.sprite.name} (last frame of set)");
        }
        else
        {
            Debug.LogWarning($"[PlayerAnimator] No sprite set available for direction {lastDir}!");
        }
    }

    private IEnumerator AnimateDirection(Vector2 dir)
    {
        Sprite[] set = GetSpriteSet(dir);
        Debug.Log($"[PlayerAnimator] AnimateDirection started. Direction: {dir}, Sprite set length: {set?.Length ?? 0}");
        
        if (set == null || set.Length == 0)
        {
            Debug.LogError($"[PlayerAnimator] No sprites available for direction {dir}! Animation cannot play.");
            yield break;
        }

        int index = 0;
        int frameCount = 0;
        while (true)
        {
            sr.sprite = set[index];
            if (frameCount < 3) // Solo log los primeros 3 frames para no saturar
            {
                Debug.Log($"[PlayerAnimator] Frame {frameCount}: Displaying sprite '{set[index].name}' (index {index}/{set.Length})");
            }
            index = (index + 1) % set.Length;
            frameCount++;
            yield return new WaitForSeconds(frameRate);
        }
    }

    private Sprite[] GetSpriteSet(Vector2 dir)
    {
        Sprite[] result = null;
        string direction = "";
        
        if (dir.y > 0) 
        {
            result = upSprites;
            direction = "UP";
        }
        else if (dir.y < 0) 
        {
            result = downSprites;
            direction = "DOWN";
        }
        else if (dir.x > 0) 
        {
            result = rightSprites;
            direction = "RIGHT";
        }
        else if (dir.x < 0) 
        {
            result = leftSprites;
            direction = "LEFT";
        }
        else
        {
            result = downSprites;
            direction = "DOWN (default)";
        }
        
        Debug.Log($"[PlayerAnimator] GetSpriteSet({dir}) -> {direction} sprites (count: {result?.Length ?? 0})");
        return result;
    }
}
