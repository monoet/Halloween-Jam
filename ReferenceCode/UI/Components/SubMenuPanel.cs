using UnityEngine;

public class SubMenuPanel : MonoBehaviour
{
    [Header("Panel Setup")]
    public GameObject contentRoot; // contenedor donde se instancian slots
    public GameObject slotPrefab;  // prefab del miembro de party
    public bool activeByDefault = false;

    void Awake()
    {
        gameObject.SetActive(activeByDefault);
    }

    public virtual void Open()
    {
        gameObject.SetActive(true);
        Refresh();
    }

    public virtual void Close()
    {
        gameObject.SetActive(false);
    }

    public virtual void Refresh()
    {
        // aquí se llenará el contenido según la party activa
    }
}
