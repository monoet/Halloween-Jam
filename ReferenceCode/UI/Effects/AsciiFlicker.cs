// Assets/Scripts/Systems/UI/AsciiFlicker.cs
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class AsciiFlicker : MonoBehaviour
{
    public float speed = 2f;
    public float intensity = 0.2f; // cuanto baja la opacidad
    private CanvasGroup cg;

    void Awake() => cg = GetComponent<CanvasGroup>();

    void Update()
    {
        float noise = Mathf.PerlinNoise(Time.time * speed, 0f);
        cg.alpha = 1f - (noise * intensity);
    }
}
