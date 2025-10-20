using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace BattleV2.UI.ActionMenu
{
    public class AttackListStrategy : MonoBehaviour, IAttackSubmenuStrategy
    {
        [SerializeField] private RectTransform listRoot;
        [SerializeField] private GameObject buttonPrefab;

        private readonly List<GameObject> spawnedButtons = new();
        private readonly List<ActionMenuOption> optionCache = new();
        private ActionMenuContext context;
        private int currentIndex;

        public void Initialise(ActionMenuContext ctx)
        {
            context = ctx;
            if (listRoot == null && context != null)
            {
                listRoot = context.Container;
            }
            Hide();
        }

        public void Show(IReadOnlyList<ActionMenuOption> options)
        {
            if (listRoot == null || buttonPrefab == null)
            {
                Debug.LogWarning("[AttackListStrategy] Missing list root or button prefab.");
                return;
            }

            ClearButtons();
            optionCache.Clear();
            optionCache.AddRange(options);
            listRoot.gameObject.SetActive(true);
            currentIndex = Mathf.Clamp(currentIndex, 0, Mathf.Max(0, optionCache.Count - 1));

            for (int i = 0; i < optionCache.Count; i++)
            {
                var option = optionCache[i];
                var go = Instantiate(buttonPrefab, listRoot);
                go.SetActive(true);

                int capturedIndex = i;
                if (go.TryGetComponent(out Button button))
                {
                    button.onClick.AddListener(() => InvokeOption(capturedIndex));
                }

                if (go.GetComponentInChildren<TMP_Text>() is TMP_Text label)
                {
                    label.text = option.DisplayName;
                }

                spawnedButtons.Add(go);
            }

            HighlightCurrent();
        }

        public void Hide()
        {
            if (listRoot != null)
            {
                listRoot.gameObject.SetActive(false);
            }
            ClearButtons();
            optionCache.Clear();
        }

        public bool HandleInput(ActionMenuInput input)
        {
            if (optionCache.Count == 0)
            {
                return false;
            }

            bool consumed = false;

            if (input.Vertical != 0)
            {
                currentIndex = Mathf.Clamp(currentIndex - input.Vertical, 0, optionCache.Count - 1);
                HighlightCurrent();
                consumed = true;
            }

            if (input.ConfirmPressed)
            {
                InvokeOption(currentIndex);
                consumed = true;
            }

            if (input.CancelPressed)
            {
                context?.OnBackRequested?.Invoke();
                consumed = true;
            }

            if (input.ChargeHeld)
            {
                context?.OnChargeRequested?.Invoke();
                consumed = true;
            }

            return consumed;
        }

        private void InvokeOption(int index)
        {
            if (index < 0 || index >= optionCache.Count)
            {
                return;
            }

            context?.OnOptionSelected?.Invoke(optionCache[index]);
        }

        private void HighlightCurrent()
        {
            for (int i = 0; i < spawnedButtons.Count; i++)
            {
                if (spawnedButtons[i] != null && spawnedButtons[i].TryGetComponent(out Selectable selectable))
                {
                    if (i == currentIndex)
                    {
                        selectable.Select();
                    }
                }
            }
        }

        private void ClearButtons()
        {
            for (int i = 0; i < spawnedButtons.Count; i++)
            {
                if (spawnedButtons[i] != null)
                {
                    Destroy(spawnedButtons[i]);
                }
            }
            spawnedButtons.Clear();
        }
    }
}
