// Assets/Scripts/Systems/GameSystemManager.cs
using UnityEngine;

public class GameSystemManager : MonoBehaviour
{
    public static GameSystemManager Instance { get; private set; }

    [Header("Referencias Globales")]
    [SerializeField] private MenuManager menuManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (menuManager == null)
            menuManager = FindObjectOfType<MenuManager>();
    }

    private void Update()
    {
        // Abrir / cerrar menú global con ESC o Start
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleMenu();
        }
    }

    public void ToggleMenu()
    {
        if (menuManager != null)
            menuManager.ToggleMenu(); // ✅ nombre correcto
    }
}
