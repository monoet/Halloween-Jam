using System.Collections;
using UnityEngine;

public class ATBBar : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CharacterRuntime runtime; // directo al personaje

    [Header("Tuning")]
    public float agiToSpeed = 10f;        // cuántos puntos/seg por cada punto de AGI
    public float minFillSpeed = 5f;       // velocidad mínima de seguridad
    public float executeDuration = 1f;    // cuánto “parpadea” al ejecutar

    private float atbValue;               // 0..100
    private float fillSpeed;              // puntos/seg
    private bool isExecuting;

    void OnEnable()  { ResetState(); }
    void Awake()     { ResetState(); }

    void Start()
    {
        if (runtime == null)
            runtime = GetComponent<CharacterRuntime>();

        float agi = (runtime != null) ? runtime.Final.AGI : 5f;
        fillSpeed = Mathf.Max(agi * agiToSpeed, minFillSpeed);
    }

    void Update()
    {
        if (isExecuting) return;

        atbValue += fillSpeed * Time.deltaTime;
        if (atbValue >= 100f)
        {
            atbValue = 100f;
            StartCoroutine(ExecuteAction());
        }
    }

    private IEnumerator ExecuteAction()
    {
        isExecuting = true;

        float t = 0f;
        while (t < executeDuration)
        {
            t += Time.deltaTime;
            yield return null;
        }

        ResetState();
    }

    private void ResetState()
    {
        isExecuting = false;
        atbValue = 0f;
    }

    // Debug overlay (IMGUI)
    void OnGUI()
    {
        if (runtime == null) return;

        var style = new GUIStyle { fontSize = 18 };
        style.normal.textColor = isExecuting ? Color.red : Color.green;

        GUILayout.BeginArea(new Rect(10, 200, 240, 90));
        GUILayout.Label($"ATB ({runtime.Final.AGI} AGI): {atbValue:0}", style);

        float w = 180f;
        GUI.Box(new Rect(10, 230, w, 20), "");
        GUI.Box(new Rect(10, 230, (atbValue / 100f) * w, 20), "");
        GUILayout.EndArea();
    }
}
