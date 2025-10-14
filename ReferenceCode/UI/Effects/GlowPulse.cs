using UnityEngine;
using TMPro;

[RequireComponent(typeof(TextMeshProUGUI))]
public class AsciiGlowPulse : MonoBehaviour
{
    [Header("Colores del glow (aliado espectral)")]
    public Color colorA = new Color(0.0f, 0.9f, 0.6f, 1f); // verde-agua
    public Color colorB = new Color(0.1f, 0.6f, 1.0f, 1f); // azul frío

    [Header("Alternativa: 'eco rojo' (enemigo)")]
    public bool redEcho = false;
    public Color redA = new Color(1.0f, 0.15f, 0.15f, 1f);
    public Color redB = new Color(1.0f, 0.45f, 0.2f, 1f);

    [Header("Pulso")]
    public float pulseSpeed = 1.6f;    // velocidad del pulso
    public float outlineMin = 0.05f;   // ancho mínimo del contorno
    public float outlineMax = 0.22f;   // ancho máximo del contorno

    [Header("Flama/Underlay")]
    public float flameDilateMin = 0.0f; // expansión interior mínima
    public float flameDilateMax = 0.35f;
    public float flameSoftMin = 0.1f;   // suavizado
    public float flameSoftMax = 0.6f;
    public float flameOffsetAmp = 0.4f; // cuánto “sube/baja” el vapor
    public float flameOffsetSpeed = 1.2f;

    private TextMeshProUGUI tmp;
    private Material runtimeMat;

    void Awake()
    {
        tmp = GetComponent<TextMeshProUGUI>();

        // MUY IMPORTANTE: instanciar el material para no afectar a todos los textos
        runtimeMat = Instantiate(tmp.fontMaterial);
        tmp.fontMaterial = runtimeMat;

        // Asegura que no haya word wrap si tu ASCII es ancho
        tmp.enableWordWrapping = false;
        tmp.richText = false;
    }

    void Update()
    {
        float t = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;

        Color glow = redEcho ? Color.Lerp(redA, redB, t)
                             : Color.Lerp(colorA, colorB, t);

        float outline = Mathf.Lerp(outlineMin, outlineMax, t);
        float dilate  = Mathf.Lerp(flameDilateMin, flameDilateMax, t);
        float soft    = Mathf.Lerp(flameSoftMin, flameSoftMax, t);
        float yOff    = Mathf.Sin(Time.time * flameOffsetSpeed) * flameOffsetAmp;

        // Outline
        runtimeMat.SetColor(ShaderUtilities.ID_OutlineColor, glow);
        runtimeMat.SetFloat(ShaderUtilities.ID_OutlineWidth, outline);

        // Underlay (sombra/glow interno)
        runtimeMat.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(glow.r, glow.g, glow.b, 0.65f));
        runtimeMat.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
        runtimeMat.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, yOff);
        runtimeMat.SetFloat(ShaderUtilities.ID_UnderlayDilate,  dilate);
        runtimeMat.SetFloat(ShaderUtilities.ID_UnderlaySoftness, soft);
    }
}
