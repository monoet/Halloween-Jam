using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Sistema de menu 100% controlado por teclado o mando.
/// No usa EventSystem ni componentes de Unity UI interactivos.
/// </summary>
public class MenuController : MonoBehaviour
{
    [Header("Entradas del menu (en orden)")]
    [SerializeField] private MenuEntry[] entries;

    [Header("Opciones")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color highlightColor = new Color(0.3f, 1f, 1f);
    [SerializeField] private float inputDelay = 0.15f;

    private int currentIndex;
    private float lastInputTime;

    private void Start()
    {
        if (entries == null || entries.Length == 0)
        {
            Debug.LogWarning("[MenuController] " + name + ": No hay entradas asignadas.");
            return;
        }

        Highlight(currentIndex);
    }

    private void Update()
    {
        if (entries == null || entries.Length == 0)
            return;

        if (Time.time - lastInputTime < inputDelay)
            return;

        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            Move(-1);
        }
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            Move(1);
        }
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Z) || Input.GetKeyDown(KeyCode.Space))
        {
            Select();
        }
    }

    private void Move(int direction)
    {
        lastInputTime = Time.time;
        Unhighlight(currentIndex);
        currentIndex = (currentIndex + direction + entries.Length) % entries.Length;
        Highlight(currentIndex);
    }

    private void Select()
    {
        if (entries == null || entries.Length == 0)
            return;

        entries[currentIndex]?.Invoke();
    }

    private void Highlight(int index)
    {
        var entry = entries[index];
        if (entry?.label != null)
            entry.label.color = highlightColor;
    }

    private void Unhighlight(int index)
    {
        var entry = entries[index];
        if (entry?.label != null)
            entry.label.color = normalColor;
    }
}

[System.Serializable]
public class MenuEntry
{
    public Text label;
    public UnityEvent onSelect;

    public void Invoke()
    {
        onSelect?.Invoke();
    }
}

