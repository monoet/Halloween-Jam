using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Lightweight VFX controller used by the combat action resolver.
/// All effects are optional; it degrades gracefully if references are missing.
/// </summary>
public class CombatVfxController : MonoBehaviour
{
    [Header("Attack Tweens")]
    [SerializeField, Min(0f)] private float lungeDistance = 0.5f;
    [SerializeField, Min(0f)] private float lungeDuration = 0.15f;
    [SerializeField, Min(0f)] private float returnDuration = 0.15f;

    [Header("Impact Tweens")]
    [SerializeField, Min(0f)] private float targetShakeScale = 0.15f;
    [SerializeField, Min(0f)] private float targetShakeDuration = 0.2f;
    [SerializeField] private Color hitFlashColor = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField, Min(0f)] private float hitFlashDuration = 0.1f;

    [Header("Floating Text")]
    [SerializeField] private Color damageTextColor = new Color(1f, 0.35f, 0.25f, 1f);
    [SerializeField] private Color healTextColor = new Color(0.25f, 1f, 0.45f, 1f);
    [SerializeField, Min(0f)] private float floatTextRise = 1f;
    [SerializeField, Min(0.1f)] private float floatTextDuration = 0.6f;
    [SerializeField] private TMP_Text floatingTextPrefab;
    [SerializeField] private Color missTextColor = new Color(1f, 1f, 1f, 0.9f);

    [Header("Crit Feedback")]
    [SerializeField] private bool enableCritRetroStyle = true;
    [SerializeField] private string critPrefix = "CRIT! ";
    [SerializeField] private Color critTextColor = new Color(1f, 0.9f, 0.2f, 1f);
    [SerializeField, Min(1f)] private float critTextScale = 1.4f;
    [SerializeField, Min(0f)] private float critJitter = 0.05f;
    [SerializeField, Min(0f)] private float critJitterDuration = 0.25f;

    [Header("Camera Shake")]
    [SerializeField] private bool cameraShakeOnCrit = true;
    [SerializeField, Min(0f)] private float camShakeDuration = 0.15f;
    [SerializeField, Min(0f)] private float camShakeStrength = 0.2f;

    [Header("Screen FX")]
    [SerializeField] private CombatScreenFxController screenFx;
    [SerializeField] private bool scanlinesOnCrit = true;

    [Header("Projectile (optional)")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField, Min(0.1f)] private float projectileSpeed = 8f;
    [SerializeField, Min(0f)] private float projectileScale = 0.2f;
    [SerializeField] private bool spawnProjectileOnAction = false;

    public bool SpawnProjectileOnAction => spawnProjectileOnAction;

    public Sequence PlayAttackFeedback(Transform attacker, Transform target)
    {
        if (attacker == null || target == null)
        {
            return null;
        }

        Vector3 dir = (target.position - attacker.position).normalized;
        Vector3 startPos = attacker.position;
        Vector3 lungePos = startPos + dir * lungeDistance;

        var sequence = DOTween.Sequence();
        sequence.Append(attacker.DOMove(lungePos, lungeDuration).SetEase(Ease.OutQuad));
        sequence.Append(attacker.DOMove(startPos, returnDuration).SetEase(Ease.InQuad));
        sequence.Join(target.DOPunchScale(Vector3.one * targetShakeScale, targetShakeDuration, vibrato: 6, elasticity: 0.8f));

        var sr = target.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            FlashSpriteRenderer(sr);
        }
        else
        {
            FlashMeshRenderer(target.GetComponentInChildren<MeshRenderer>());
        }

        return sequence;
    }

    public Sequence PlayProjectileShot(Transform attacker, Transform target)
    {
        if (attacker == null || target == null)
        {
            return null;
        }

        var projectile = projectilePrefab != null
            ? Instantiate(projectilePrefab)
            : GameObject.CreatePrimitive(PrimitiveType.Capsule);

        if (projectilePrefab == null)
        {
            projectile.transform.localScale = Vector3.one * projectileScale;
            var collider = projectile.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
        }

        projectile.transform.position = attacker.position;

        float distance = Vector3.Distance(attacker.position, target.position);
        float duration = Mathf.Max(0.05f, distance / Mathf.Max(0.01f, projectileSpeed));

        var sequence = DOTween.Sequence();
        sequence.Append(projectile.transform.DOMove(target.position, duration).SetEase(Ease.Linear));
        sequence.OnComplete(() => Destroy(projectile));
        return sequence;
    }

    public void ShowDamageNumber(int amount, Vector3 worldPos, bool isHeal)
    {
        string text = amount.ToString();
        Color color = isHeal ? healTextColor : damageTextColor;
        SpawnFloatingText(text, worldPos, color);
    }

    public void ShowDamageNumber(int amount, Vector3 worldPos, bool isHeal, bool isCrit)
    {
        if (enableCritRetroStyle && isCrit)
        {
            var tmp = SpawnFloatingText(critPrefix + amount, worldPos, critTextColor, returnInstance: true);
            if (tmp != null)
            {
                tmp.transform.localScale *= critTextScale;
                tmp.transform.DOPunchPosition(new Vector3(Random.Range(-critJitter, critJitter), Random.Range(-critJitter, critJitter), 0f), critJitterDuration, vibrato: 8, elasticity: 0.9f);
                if (cameraShakeOnCrit && Camera.main != null)
                {
                    Camera.main.transform.DOShakePosition(camShakeDuration, camShakeStrength, vibrato: 10, randomness: 45f, fadeOut: true);
                }

                if (scanlinesOnCrit)
                {
                    if (screenFx == null)
                    {
                        screenFx = FindObjectOfType<CombatScreenFxController>(includeInactive: true);
                    }

                    screenFx?.PlayScanlinesFlash();
                }
            }

            return;
        }

        ShowDamageNumber(amount, worldPos, isHeal);
    }

    public void ShowMiss(Vector3 worldPos)
    {
        var tmp = SpawnFloatingText("MISS!", worldPos, missTextColor, returnInstance: true);
        tmp?.transform.DOPunchPosition(new Vector3(0.15f, 0f, 0f), 0.3f, vibrato: 10, elasticity: 0.9f);
    }

    private TMP_Text SpawnFloatingText(string text, Vector3 worldPos, Color color, bool returnInstance = false)
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
        }

        tmp.text = text;
        tmp.color = color;
        tmp.transform.position = worldPos;

        Vector3 start = tmp.transform.position;
        Vector3 end = start + Vector3.up * floatTextRise;

        tmp.DOFade(0f, floatTextDuration).SetEase(Ease.OutSine);
        var moveTween = tmp.transform.DOMove(end, floatTextDuration).SetEase(Ease.OutQuad);
        moveTween.OnComplete(() =>
        {
            if (tmp != null)
            {
                Destroy(tmp.gameObject);
            }
        });

        return returnInstance ? tmp : null;
    }

    private void FlashSpriteRenderer(SpriteRenderer sr)
    {
        if (sr == null)
        {
            return;
        }

        Color original = sr.color;
        sr.DOColor(hitFlashColor, hitFlashDuration)
          .OnComplete(() => sr.DOColor(original, hitFlashDuration));
    }

    private void FlashMeshRenderer(MeshRenderer mr)
    {
        if (mr == null || mr.material == null || !mr.material.HasProperty("_Color"))
        {
            return;
        }

        var mat = mr.material;
        Color original = mat.color;
        DOTween.To(() => mat.color, c => mat.color = c, hitFlashColor, hitFlashDuration)
               .OnComplete(() => DOTween.To(() => mat.color, c => mat.color = c, original, hitFlashDuration));
    }
}
