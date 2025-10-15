using UnityEngine;

/// <summary>
/// Control de animación simple para movimiento por grilla.
/// - Reproduce animaciones direccionales.
/// - Se congela al detenerse.
/// - Permite definir un sprite o clip inicial sin animación.
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerAnimatorController : MonoBehaviour
{
    [Header("Idle inicial")]
    [Tooltip("Clip de animación a mostrar al iniciar (opcional).")]
    [SerializeField] private AnimationClip initialClip;

    [Tooltip("Sprite a mostrar si no se define un clip inicial.")]
    [SerializeField] private Sprite initialSprite;

    private Animator anim;
    private SpriteRenderer sr;
    private string currentClip;

    private void Awake()
    {
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        if (initialClip != null)
        {
            // Si se asignó un clip inicial: reproducir primer frame y congelar
            anim.Play(initialClip.name, 0, 0f);
            anim.speed = 0f;
            currentClip = initialClip.name;
        }
        else if (initialSprite != null)
        {
            // Si no hay clip, mostrar sprite manualmente (sin Animator activo)
            anim.enabled = false;
            sr.sprite = initialSprite;
        }
    }

    public void PlayMoveAnimation(Vector2Int dir)
    {
        // Si el Animator estaba desactivado, lo reactivamos
        if (!anim.enabled)
        {
            anim.enabled = true;
            anim.speed = 1f;
        }

        string clip = GetClipName(dir);

        if (clip != currentClip)
        {
            anim.Play(clip);
            currentClip = clip;
        }

        anim.speed = 1f;
    }

    public void StopAnimation()
    {
        if (!anim.enabled) return;
        anim.speed = 0f;
        // Si prefieres volver al primer frame:
        // anim.Play(currentClip, 0, 0f);
    }

    private string GetClipName(Vector2Int dir)
    {
        if (dir.x > 0) return "Walk_Right";
        if (dir.x < 0) return "Walk_Left";
        if (dir.y > 0) return "Walk_Up";
        return "Walk_Down";
    }
}
