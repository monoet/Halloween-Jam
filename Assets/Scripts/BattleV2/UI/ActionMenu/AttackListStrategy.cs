using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using BattleV2.UI;
using DG.Tweening;

namespace BattleV2.UI.ActionMenu
{
    public class AttackListStrategy : MonoBehaviour, IAttackSubmenuStrategy
    {
        [SerializeField] private RectTransform listRoot;
        [SerializeField] private GameObject buttonPrefab;

        [SerializeField] private float highlightScale = 1.12f;
        [SerializeField] private float highlightDuration = 0.12f;
        [SerializeField] private Ease highlightEase = Ease.OutQuad;
        private readonly List<GameObject> spawnedButtons = new();
        private readonly List<ActionMenuOption> optionCache = new();
        private ActionMenuContext context;
        private int currentIndex;
        private bool usingDynamicButtons;

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
            if (listRoot == null)
            {
                Debug.LogWarning("[AttackListStrategy] Missing list root.");
                return;
            }

            optionCache.Clear();
            if (options != null)
            {
                optionCache.AddRange(options);
            }

            usingDynamicButtons = optionCache.Count > 0;

            if (usingDynamicButtons && buttonPrefab == null)
            {
                Debug.LogWarning("[AttackListStrategy] No button prefab assigned for dynamic options.");
                return;
            }

            ClearButtons(destroyObjects: usingDynamicButtons);
            optionCache.Clear();
            listRoot.gameObject.SetActive(true);

            if (usingDynamicButtons)
            {
                optionCache.AddRange(options);
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
                    EnsureFocusTween(go);
                    EnsureConfirmTween(go);
                }
            }
            else
            {
                PopulateFromExisting();
            }

            currentIndex = Mathf.Clamp(currentIndex, 0, Mathf.Max(0, spawnedButtons.Count - 1));

            HighlightCurrent();
        }

        public void Hide()
        {
            if (listRoot != null)
            {
                listRoot.gameObject.SetActive(false);
            }
            ClearButtons(destroyObjects: usingDynamicButtons);
            optionCache.Clear();
            usingDynamicButtons = false;
        }

        public bool HandleInput(ActionMenuInput input)
        {
            if (spawnedButtons.Count == 0)
            {
                return false;
            }

            bool consumed = false;

            if (input.Vertical != 0)
            {
                currentIndex = Mathf.Clamp(currentIndex - input.Vertical, 0, spawnedButtons.Count - 1);
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
            if (index < 0 || index >= spawnedButtons.Count)
            {
                return;
            }

            if (usingDynamicButtons)
            {
                if (index < optionCache.Count)
                {
                    context?.OnOptionSelected?.Invoke(optionCache[index]);
                }
                return;
            }

            if (spawnedButtons[index] != null && spawnedButtons[index].TryGetComponent(out Button button))
            {
                button.onClick?.Invoke();
            }
        }

        private void HighlightCurrent()
        {
            if (spawnedButtons.Count == 0)
            {
                return;
            }

            Selectable selected = null;
            for (int i = 0; i < spawnedButtons.Count; i++)
            {
                var go = spawnedButtons[i];
                if (go == null)
                {
                    continue;
                }

                if (go.TryGetComponent(out Selectable selectable))
                {
                    if (i == currentIndex)
                    {
                        selected = selectable;
                        selectable.Select();
                    }
                }
            }

            if (selected != null && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(selected.gameObject);
            }
        }

        private void ClearButtons(bool destroyObjects)
        {
            for (int i = 0; i < spawnedButtons.Count; i++)
            {
                var go = spawnedButtons[i];
                if (go == null)
                {
                    continue;
                }

                ResetFocusTween(go);

                if (destroyObjects)
                {
                    Destroy(go);
                }
            }
            spawnedButtons.Clear();
        }

        private void PopulateFromExisting()
        {
            spawnedButtons.Clear();

            if (listRoot == null)
            {
                return;
            }

            for (int i = 0; i < listRoot.childCount; i++)
            {
                var child = listRoot.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                var go = child.gameObject;
                if (!go.activeInHierarchy)
                {
                    go.SetActive(true);
                }

                spawnedButtons.Add(go);
                EnsureFocusTween(go);
                EnsureConfirmTween(go);
            }

            if (spawnedButtons.Count == 0)
            {
                Debug.LogWarning("[AttackListStrategy] No buttons found under list root.");
            }
        }

        private void EnsureConfirmTween(GameObject buttonObject)
        {
            if (buttonObject == null)
            {
                return;
            }

            if (!buttonObject.TryGetComponent<ButtonConfirmTween>(out _))
            {
                buttonObject.AddComponent<ButtonConfirmTween>();
            }
        }

        private void EnsureFocusTween(GameObject buttonObject)
        {
            if (buttonObject == null)
            {
                return;
            }

            var focusTween = buttonObject.GetComponent<ButtonFocusTween>();
            if (focusTween == null)
            {
                focusTween = buttonObject.AddComponent<ButtonFocusTween>();
            }

            focusTween.Configure(highlightScale, highlightDuration, highlightEase);

            if (buttonObject.GetComponentInChildren<TMP_Text>() is TMP_Text text)
            {
                focusTween.SetTarget(text.rectTransform);
            }
            else if (buttonObject.transform is RectTransform rect)
            {
                focusTween.SetTarget(rect);
            }
        }

        private void ResetFocusTween(GameObject buttonObject)
        {
            if (buttonObject == null)
            {
                return;
            }

            if (buttonObject.TryGetComponent<ButtonFocusTween>(out var focusTween))
            {
                focusTween.ResetImmediate();
            }
        }
    }
}
