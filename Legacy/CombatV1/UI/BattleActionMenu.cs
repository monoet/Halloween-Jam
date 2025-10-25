using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace HalloweenJam.UI.Combat
{
    public class BattleActionMenu : MonoBehaviour
    {
        private enum PanelState
        {
            None,
            Root,
            Attack
        }

        [Header("Root Menu")]
        [SerializeField] private GameObject rootMenuRoot;
        [SerializeField] private Button attackButton;
        [SerializeField] private Button itemButton;
        [SerializeField] private Button magicButton;
        [SerializeField] private Button defendButton;
        [SerializeField] private Button fleeButton;

        [Header("Attack Menu")]
        [SerializeField] private GameObject attackMenuRoot;
        [SerializeField] private Button attackConfirmButton;
        [SerializeField] private Button attackBackButton;

        [Header("Optional Labels")]
        [SerializeField] private TMP_Text actionDescriptionLabel;

        [Header("Behavior")]
        [SerializeField] private bool autoHideOnConfirm = true;

        [Header("Animation")]
        [SerializeField] private float showDuration = 0.25f;
        [SerializeField] private float hideDuration = 0.15f;
        [SerializeField] private Ease showScaleEase = Ease.OutBack;
        [SerializeField] private Ease showFadeEase = Ease.OutQuad;
        [SerializeField] private Ease hideEase = Ease.InQuad;

        public event Action OnAttackConfirmed;
        public event Action OnCancel;

        private readonly List<Button> orderedButtons = new();
        private readonly Dictionary<Button, UnityAction> clickHandlers = new();
        private PanelState currentPanel = PanelState.None;
        private int currentIndex;
        private bool menuActive;

        private void Awake()
        {
            HideAllPanelsImmediate();
            ClearSelection();
        }

        private void Update()
        {
            if (!menuActive)
            {
                return;
            }

            HandleNavigation();
            HandleConfirm();
            HandleCancel();
        }

        public void RegisterAttackCallback(Action callback)
        {
            OnAttackConfirmed -= callback;
            OnAttackConfirmed += callback;
        }

        public void ClearAttackCallbacks()
        {
            OnAttackConfirmed = null;
        }

        public void ShowMenu()
        {
            menuActive = true;
            SwitchToPanel(PanelState.Root, animate: true);
        }

        public void HideMenu()
        {
            if (!menuActive)
            {
                return;
            }

            menuActive = false;

            var currentRoot = GetPanelRoot(currentPanel);
            AnimatePanel(currentRoot, false, hideDuration, () =>
            {
                currentPanel = PanelState.None;
                HideAllPanelsImmediate();
                ClearSelection();
            });
        }

        private void HandleNavigation()
        {
            int direction = 0;

            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A))
            {
                direction = -1;
            }
            else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D))
            {
                direction = 1;
            }

            if (direction == 0 || orderedButtons.Count == 0)
            {
                return;
            }

            MoveSelection(direction);
        }

        private void HandleConfirm()
        {
            if (!Input.GetKeyDown(KeyCode.Q))
            {
                return;
            }

            HandleButtonClick(GetCurrentButton());
        }

        private void HandleCancel()
        {
            if (!Input.GetKeyDown(KeyCode.E))
            {
                return;
            }

            if (currentPanel == PanelState.Attack)
            {
                SwitchToPanel(PanelState.Root, animate: true);
                return;
            }

            OnCancel?.Invoke();
            HideMenu();
        }

        private void HandleButtonClick(Button button)
        {
            if (button == null || !button.interactable)
            {
                return;
            }

            switch (currentPanel)
            {
                case PanelState.Root:
                    if (button == attackButton)
                    {
                        if (attackMenuRoot != null && attackConfirmButton != null)
                        {
                            SwitchToPanel(PanelState.Attack, animate: true);
                        }
                        else
                        {
                            TriggerAttackConfirm();
                        }
                    }
                    break;
                case PanelState.Attack:
                    if (button == attackConfirmButton)
                    {
                        TriggerAttackConfirm();
                    }
                    else if (button == attackBackButton)
                    {
                        SwitchToPanel(PanelState.Root, animate: true);
                    }
                    break;
            }
        }

        internal void NotifyPointerEnter(Button button)
        {
            if (orderedButtons.Count == 0)
            {
                return;
            }

            int index = orderedButtons.IndexOf(button);
            if (index >= 0)
            {
                currentIndex = index;
            }

            FocusButton(button);
        }

        private void TriggerAttackConfirm()
        {
            OnAttackConfirmed?.Invoke();

            if (autoHideOnConfirm)
            {
                HideMenu();
            }
        }

        private void SwitchToPanel(PanelState targetPanel, bool animate)
        {
            var nextRoot = GetPanelRoot(targetPanel);
            if (nextRoot == null)
            {
                Debug.LogWarning($"BattleActionMenu: Panel {targetPanel} has no root assigned.", this);
                return;
            }

            if (currentPanel == targetPanel && nextRoot.activeSelf)
            {
                return;
            }

            GameObject currentRoot = GetPanelRoot(currentPanel);

            void ShowNextPanel()
            {
                currentPanel = targetPanel;
                BuildButtonListForCurrentPanel();
                AnimatePanel(nextRoot, true, animate ? showDuration : 0f, FocusFirstInteractableButton);
            }

            if (currentRoot != null && currentRoot.activeSelf)
            {
                AnimatePanel(currentRoot, false, animate ? hideDuration : 0f, ShowNextPanel);
            }
            else
            {
                ShowNextPanel();
            }
        }

        private void MoveSelection(int direction)
        {
            int attempts = 0;
            int newIndex = currentIndex;

            while (attempts < orderedButtons.Count)
            {
                newIndex = (newIndex + direction + orderedButtons.Count) % orderedButtons.Count;
                attempts++;

                var candidate = orderedButtons[newIndex];
                if (candidate != null && candidate.interactable)
                {
                    currentIndex = newIndex;
                    FocusButton(candidate);
                    return;
                }
            }
        }

        private void FocusFirstInteractableButton()
        {
            if (orderedButtons.Count == 0)
            {
                ClearSelection();
                return;
            }

            for (int i = 0; i < orderedButtons.Count; i++)
            {
                var button = orderedButtons[i];
                if (button != null && button.interactable)
                {
                    currentIndex = i;
                    FocusButton(button);
                    return;
                }
            }

            ClearSelection();
        }

        private void FocusButton(Button button)
        {
            if (button == null)
            {
                return;
            }

            if (EventSystem.current != null)
            {
                var currentSelected = EventSystem.current.currentSelectedGameObject;
                if (currentSelected != button.gameObject)
                {
                    EventSystem.current.SetSelectedGameObject(button.gameObject);
                }
            }

            UpdateDescriptionLabel(button);
        }

        private void UpdateDescriptionLabel(Button button)
        {
            if (actionDescriptionLabel == null)
            {
                return;
            }

            if (button == null)
            {
                actionDescriptionLabel.text = string.Empty;
                actionDescriptionLabel.gameObject.SetActive(false);
                return;
            }

            var label = button.GetComponentInChildren<TMP_Text>(true);
            var text = label != null ? label.text : string.Empty;

            actionDescriptionLabel.text = text;
            actionDescriptionLabel.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        private Button GetCurrentButton()
        {
            if (orderedButtons.Count == 0 || currentIndex < 0 || currentIndex >= orderedButtons.Count)
            {
                return null;
            }

            return orderedButtons[currentIndex];
        }

        private void BuildButtonListForCurrentPanel()
        {
            orderedButtons.Clear();

            switch (currentPanel)
            {
                case PanelState.Root:
                    AppendButton(attackButton, true);
                    AppendButton(itemButton, false);
                    AppendButton(magicButton, false);
                    AppendButton(defendButton, false);
            AppendButton(fleeButton, false);
            break;
        case PanelState.Attack:
            AppendButton(attackConfirmButton, true);
            AppendButton(attackBackButton, true);
            break;
        }

        currentIndex = 0;
    }

        private void AppendButton(Button button, bool canSelect)
        {
            if (button == null)
            {
                return;
            }

            button.interactable = canSelect;
            orderedButtons.Add(button);
            SetupButtonListeners(button);
        }

        private void ClearSelection()
        {
            currentIndex = 0;
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            UpdateDescriptionLabel(null);
        }

        private void HideAllPanelsImmediate()
        {
            if (rootMenuRoot != null)
            {
                rootMenuRoot.SetActive(false);
            }

            if (attackMenuRoot != null)
            {
                attackMenuRoot.SetActive(false);
            }
        }

        private GameObject GetPanelRoot(PanelState panel)
        {
            return panel switch
            {
                PanelState.Root => rootMenuRoot,
                PanelState.Attack => attackMenuRoot,
                _ => null
            };
        }

        private void AnimatePanel(GameObject panel, bool show, float duration, Action onComplete)
        {
            if (panel == null)
            {
                onComplete?.Invoke();
                return;
            }

            var canvasGroup = panel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = panel.AddComponent<CanvasGroup>();
            }

            canvasGroup.DOKill();
            panel.transform.DOKill();

            if (duration <= 0f)
            {
                if (show)
                {
                    panel.SetActive(true);
                    canvasGroup.alpha = 1f;
                    panel.transform.localScale = Vector3.one;
                }
                else
                {
                    canvasGroup.alpha = 0f;
                    panel.transform.localScale = Vector3.zero;
                    panel.SetActive(false);
                }

                onComplete?.Invoke();
                return;
            }

            if (show)
            {
                panel.SetActive(true);
                canvasGroup.alpha = 0f;
                panel.transform.localScale = Vector3.zero;

                canvasGroup.DOFade(1f, duration).SetEase(showFadeEase);
                panel.transform.DOScale(Vector3.one, duration).SetEase(showScaleEase)
                    .OnComplete(() => onComplete?.Invoke());
            }
            else
            {
                canvasGroup.DOFade(0f, duration).SetEase(hideEase);
                panel.transform.DOScale(Vector3.zero, duration).SetEase(hideEase)
                    .OnComplete(() =>
                    {
                        panel.SetActive(false);
                        onComplete?.Invoke();
                    });
            }
        }

        private void SetupButtonListeners(Button button)
        {
            if (button == null)
            {
                return;
            }

            if (!clickHandlers.TryGetValue(button, out var handler))
            {
                handler = () => HandleButtonClick(button);
                clickHandlers[button] = handler;
            }

            button.onClick.RemoveListener(handler);
            button.onClick.AddListener(handler);

            var hover = button.GetComponent<BattleActionMenuHover>();
            if (hover == null)
            {
                hover = button.gameObject.AddComponent<BattleActionMenuHover>();
            }

            hover.Initialize(this, button);
        }
    }
}

