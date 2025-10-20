using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BattleV2.UI
{
    /// <summary>
    /// Ensures a specific selectable receives focus whenever the object is enabled.
    /// Useful for automatically selecting an invisible button in HUD view so confirm input works immediately.
    /// </summary>
    public class UISetSelectedOnEnable : MonoBehaviour
    {
        [SerializeField] private Selectable target;
        [SerializeField] private bool selectOnStart = false;

        private void Awake()
        {
            if (target == null)
            {
                target = GetComponent<Selectable>();
            }
        }

        private void Start()
        {
            if (selectOnStart)
            {
                SetSelection();
            }
        }

        private void OnEnable()
        {
            SetSelection();
        }

        private void SetSelection()
        {
            if (target == null)
            {
                return;
            }

            var eventSystem = EventSystem.current;
            if (eventSystem != null)
            {
                eventSystem.SetSelectedGameObject(target.gameObject);
            }
        }
    }
}
