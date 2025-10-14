// Assets/Scripts/Systems/UI/AsciiAnimator.cs
using UnityEngine;
using TMPro;

public class AsciiAnimator : MonoBehaviour
{
    [Header("Data (ScriptableObject)")]
    [SerializeField] private AsciiCharacterData data;

    [Header("UI Target")]
    [SerializeField] private TextMeshProUGUI asciiText;

    private int currentFrame;
    private float timer;

    private void Reset()
    {
        // Autodetecta el TextMeshPro en este GameObject si existe
        asciiText = GetComponent<TextMeshProUGUI>();
    }

    private void Update()
    {
        if (data == null || asciiText == null || data.frames.Length == 0) return;

        // 🔑 Calcula duración de cada frame en segundos según FPS
        float frameDuration = 1f / Mathf.Max(0.01f, data.frameRate);

        timer += Time.deltaTime;
        if (timer >= frameDuration)
        {
            timer = 0f;
            currentFrame++;

            if (currentFrame >= data.frames.Length)
            {
                if (data.loop) currentFrame = 0;
                else currentFrame = data.frames.Length - 1;
            }

            string newFrame = data.frames[currentFrame];
            asciiText.text = newFrame;

            // 🔎 Debug en consola
            Debug.Log($"[AsciiAnimator] Frame {currentFrame}: \n{newFrame}");
        }
    }

    public void SetData(AsciiCharacterData newData)
    {
        data = newData;
        currentFrame = 0;
        timer = 0f;

        if (asciiText != null && data.frames.Length > 0)
        {
            asciiText.text = data.frames[0];
            Debug.Log($"[AsciiAnimator] Init with frame 0: \n{data.frames[0]}");
        }
    }
}
