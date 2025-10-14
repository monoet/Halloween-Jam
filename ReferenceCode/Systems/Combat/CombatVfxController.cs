using UnityEngine;
using DG.Tweening;
using TMPro;

/// <summary>
/// Controlador simple de VFX/tweens para feedback de acciones.
/// No bloquea el flujo: ejecuta tweens en paralelo.
/// </summary>
public class CombatVfxController : MonoBehaviour
{
    [Header("Tweens de ataque")]
    [SerializeField, Min(0f)] private float lungeDistance = 0.5f;
    [SerializeField, Min(0f)] private float lungeDuration = 0.15f;
    [SerializeField, Min(0f)] private float returnDuration = 0.15f;

    [Header("Tweens de impacto")]
    [SerializeField, Min(0f)] private float targetShakeScale = 0.15f;
    [SerializeField, Min(0f)] private float targetShakeDuration = 0.2f;
    [SerializeField] private Color hitFlashColor = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField, Min(0f)] private float hitFlashDuration = 0.1f;

    [Header("Floating Text (daño/curación)")]
    [SerializeField] private Color damageTextColor = new Color(1f, 0.35f, 0.25f, 1f);
    [SerializeField] private Color healTextColor = new Color(0.25f, 1f, 0.45f, 1f);
    [SerializeField, Min(0f)] private float floatTextRise = 1.0f;
    [SerializeField, Min(0.1f)] private float floatTextDuration = 0.6f;
    [SerializeField] private TMP_Text floatingTextPrefab;
    [SerializeField] private Color missTextColor = new Color(1f, 1f, 1f, 0.9f);

    [Header("CRIT Retro Style")]
    [SerializeField] private bool enableCritRetroStyle = true;
    [SerializeField] private string critPrefix = "CRIT! ";
    [SerializeField] private Color critTextColor = new Color(1f, 0.9f, 0.2f, 1f);
    [SerializeField, Min(1f)] private float critTextScale = 1.4f;
    [SerializeField, Min(0f)] private float critJitter = 0.05f;
    [SerializeField, Min(0f)] private float critJitterDuration = 0.25f;

    [Header("Camera Shake (retro)")]
    [SerializeField] private bool cameraShakeOnCrit = true;
    [SerializeField, Min(0f)] private float camShakeDuration = 0.15f;
    [SerializeField, Min(0f)] private float camShakeStrength = 0.2f;

    [Header("Screen FX (retro)")]
    [SerializeField] private CombatScreenFxController screenFx;
    [SerializeField] private bool scanlinesOnCrit = true;

    [Header("Proyectil (opcional)")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField, Min(0.1f)] private float projectileSpeed = 8f;
    [SerializeField, Min(0f)] private float projectileScale = 0.2f;
    [SerializeField] private bool spawnProjectileOnAction = false;

    public bool SpawnProjectileOnAction => spawnProjectileOnAction;

    public DG.Tweening.Sequence PlayAttackFeedback(Transform attacker, Transform target)
    {
        if (attacker == null || target == null) return null;

        // Lunge del atacante hacia el objetivo y regreso
        Vector3 dir = (target.position - attacker.position).normalized;
        Vector3 startPos = attacker.position;
        Vector3 lungePos = startPos + dir * lungeDistance;

        var seq = DOTween.Sequence();
        seq.Append(attacker.DOMove(lungePos, lungeDuration).SetEase(Ease.OutQuad));
        seq.Append(attacker.DOMove(startPos, returnDuration).SetEase(Ease.InQuad));

        // Impact: shake/flash in paralelo
        seq.Join(target.DOPunchScale(Vector3.one * targetShakeScale, targetShakeDuration, vibrato: 6, elasticity: 0.8f));

        var sr = target.GetComponent<SpriteRenderer>();
        if (sr != null)
            seq.Join(DOTween.To(() => sr.color, c => sr.color = c, hitFlashColor, hitFlashDuration)
                             .OnComplete(() => DOTween.To(() => sr.color, c => sr.color = c, Color.white, hitFlashDuration)));
        else
        {
            var mr = target.GetComponentInChildren<MeshRenderer>();
            if (mr != null && mr.material != null && mr.material.HasProperty("_Color"))
            {
                var mat = mr.material;
                Color original = mat.color;
                seq.Join(DOTween.To(() => mat.color, c => mat.color = c, hitFlashColor, hitFlashDuration)
                                 .OnComplete(() => DOTween.To(() => mat.color, c => mat.color = c, original, hitFlashDuration)));
            }
        }

        return seq;
    }

    public DG.Tweening.Sequence PlayProjectileShot(Transform attacker, Transform target)
    {
        var seq = DOTween.Sequence();
        if (attacker == null || target == null) return seq;

        GameObject proj;
        if (projectilePrefab != null)
            proj = Instantiate(projectilePrefab);
        else
        {
            proj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            proj.transform.localScale = Vector3.one * projectileScale;
            var col = proj.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        proj.transform.position = attacker.position;
        float dist = Vector3.Distance(attacker.position, target.position);
        float duration = Mathf.Max(0.05f, dist / Mathf.Max(0.01f, projectileSpeed));
        seq.Append(proj.transform.DOMove(target.position, duration).SetEase(Ease.Linear));
        seq.OnComplete(() => Destroy(proj));
        return seq;
    }

    private void FlashSpriteRenderer(SpriteRenderer sr)
    {
        if (sr == null) return;
        Color original = sr.color;
        sr.DOColor(hitFlashColor, hitFlashDuration).OnComplete(() => sr.DOColor(original, hitFlashDuration));
    }

    private void FlashMeshRenderer(MeshRenderer mr)
    {
        if (mr == null || mr.material == null) return;
        var mat = mr.material;
        if (mat.HasProperty("_Color"))
        {
            Color original = mat.color;
            DOTween.To(() => mat.color, c => mat.color = c, hitFlashColor, hitFlashDuration)
                   .OnComplete(() => DOTween.To(() => mat.color, c => mat.color = c, original, hitFlashDuration));
        }
    }

    public void ShowDamageNumber(int amount, Vector3 worldPos, bool isHeal)
    {
        string text = amount.ToString();
        Color col = isHeal ? healTextColor : damageTextColor;
        SpawnFloatingText(text, worldPos, col);
    }

    public void ShowDamageNumber(int amount, Vector3 worldPos, bool isHeal, bool isCrit)
    {
        if (enableCritRetroStyle && isCrit)
        {
            string text = critPrefix + amount.ToString();
            // Spawn text and apply retro-ish effects
            var tmp = SpawnFloatingText(text, worldPos, critTextColor, returnInstance: true);
            if (tmp != null)
            {
                // Bigger size
                tmp.transform.localScale *= critTextScale;
                // Jitter punch
                tmp.transform.DOPunchPosition(new Vector3(Random.Range(-critJitter, critJitter), Random.Range(-critJitter, critJitter), 0f), critJitterDuration, vibrato: 8, elasticity: 0.9f);
                // Optional camera shake
                if (cameraShakeOnCrit && Camera.main != null)
                {
                    Camera.main.transform.DOShakePosition(camShakeDuration, camShakeStrength, vibrato: 10, randomness: 45f, snapping: false, fadeOut: true);
                }
                if (scanlinesOnCrit)
                {
                    if (screenFx == null) screenFx = FindObjectOfType<CombatScreenFxController>(true);
                    if (screenFx != null) screenFx.PlayScanlinesFlash();
                }
            }
            return;
        }

        ShowDamageNumber(amount, worldPos, isHeal);
    }

    public void SpawnFloatingText(string text, Vector3 worldPos, Color color)
        => SpawnFloatingText(text, worldPos, color, returnInstance: false);

    public TMP_Text SpawnFloatingText(string text, Vector3 worldPos, Color color, bool returnInstance)
    {
        TMP_Text tmp;
        if (floatingTextPrefab != null)
        {
            tmp = Instantiate(floatingTextPrefab);
        }
        else
        {
            var go = new GameObject("FloatingText");
            tmp = go.AddComponent<TextMeshPro>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 3f;
            tmp.enableAutoSizing = false;
        }

        tmp.text = text;
        tmp.color = color;
        tmp.transform.position = worldPos;

        var start = tmp.transform.position;
        var end = start + Vector3.up * floatTextRise;

        tmp.DOFade(0f, floatTextDuration).SetEase(Ease.OutSine);
        var moveTween = tmp.transform.DOMove(end, floatTextDuration).SetEase(Ease.OutQuad);
        moveTween.OnComplete(() => { if (tmp != null) Destroy(tmp.gameObject); });

        return returnInstance ? tmp : null;
    }

    public void ShowMiss(Vector3 worldPos)
    {
        var tmp = SpawnFloatingText("MISS!", worldPos, missTextColor, returnInstance: true);
        if (tmp != null)
        {
            // Retro-ish horizontal shake
            tmp.transform.DOPunchPosition(new Vector3(0.15f, 0f, 0f), 0.3f, vibrato: 10, elasticity: 0.9f);
        }
    }
}
