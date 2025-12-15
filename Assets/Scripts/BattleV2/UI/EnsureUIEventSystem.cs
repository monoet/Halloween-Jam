using UnityEngine;
using UnityEngine.EventSystems;

namespace BattleV2.UI
{
    /// <summary>
    /// Asegura que exista un EventSystem en escena para navegación UI.
    /// Útil cuando la escena no lo tiene en la jerarquía.
    /// </summary>
    public sealed class EnsureUIEventSystem : MonoBehaviour
    {
        private void Awake()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(go);
        }
    }
}
