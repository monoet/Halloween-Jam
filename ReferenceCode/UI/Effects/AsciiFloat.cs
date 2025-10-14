// Assets/Scripts/Systems/UI/AsciiFloat.cs
using UnityEngine;

public class AsciiFloat : MonoBehaviour
{
    [Header("Float settings")]
    public float amplitude = 5f;  // cuánto sube/baja en píxeles
    public float speed = 2f;      // velocidad de oscilación

    private RectTransform rect;
    private Vector2 startPos;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        startPos = rect.anchoredPosition;
    }

    void Update()
    {
        float offset = Mathf.Sin(Time.time * speed) * amplitude;
        rect.anchoredPosition = startPos + new Vector2(0, offset);
    }
}
