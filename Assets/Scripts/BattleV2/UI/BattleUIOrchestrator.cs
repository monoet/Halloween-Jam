using System.Collections;
using System.Collections.Generic;
using BattleV2.Anim;
using DG.Tweening;
using UnityEngine;

namespace BattleV2.UI
{
    /// <summary>
    /// Handles visual transitions between battle menus and responds to combat locks.
    /// </summary>
    public class BattleUIOrchestrator : MonoBehaviour
    {
        [SerializeField] private float fadeTime = 0.25f;
        [SerializeField] private Ease fadeEase = Ease.OutQuad;
        [SerializeField] private CanvasGroup[] menuGroups;

        private const string DebugTag = "[BattleUI]";

        private readonly Dictionary<string, CanvasGroup> menuLookup = new();
        private CanvasGroup current;
        private bool locked;
        private Coroutine switchRoutine;

        private void Awake()
        {
            foreach (var group in menuGroups)
            {
                if (group == null)
                {
                    continue;
                }

                var key = group.gameObject.name;
                if (!menuLookup.ContainsKey(key))
                {
                    menuLookup.Add(key, group);
                }

                group.alpha = group.gameObject.activeSelf ? 1f : 0f;
                group.blocksRaycasts = group.gameObject.activeSelf;

                if (group.gameObject.activeSelf)
                {
                    current = group;
                }
            }
        }

        private void OnEnable()
        {
            BattleEvents.OnLockChanged += HandleLockChanged;
        }

        private void OnDisable()
        {
            BattleEvents.OnLockChanged -= HandleLockChanged;
            if (switchRoutine != null)
            {
                StopCoroutine(switchRoutine);
                switchRoutine = null;
            }
        }

        public void ShowMenu(string menuName)
        {
            if (locked || string.IsNullOrWhiteSpace(menuName))
            {
                return;
            }

            if (!menuLookup.TryGetValue(menuName, out var next))
            {
                Debug.LogWarning($"[BattleUI] Menu '{menuName}' not registered.");
                return;
            }

            ShowMenu(next);
        }

        public void ShowMenu(CanvasGroup next)
        {
            if (locked || next == null || next == current)
            {
                return;
            }

            if (switchRoutine != null)
            {
                StopCoroutine(switchRoutine);
            }

            switchRoutine = StartCoroutine(SwitchRoutine(next));
        }

        private IEnumerator SwitchRoutine(CanvasGroup next)
        {
            Debug.Log($"{DebugTag} Switch -> {next.name}");
            if (current != null)
            {
                yield return FadeOut(current);
            }

            current = next;
            yield return FadeIn(current);
            switchRoutine = null;
        }

        private IEnumerator FadeIn(CanvasGroup target)
        {
            
            target.blocksRaycasts = true;
            Debug.Log($"{DebugTag} FadeIn {target.name}");
            yield return target.DOFade(1f, fadeTime).SetEase(fadeEase).WaitForCompletion();
        }

        private IEnumerator FadeOut(CanvasGroup target)
        {
            target.blocksRaycasts = false;
            Debug.Log($"{DebugTag} FadeOut {target.name}");
            yield return target.DOFade(0f, fadeTime).SetEase(fadeEase).WaitForCompletion();
        }

        private void HandleLockChanged(bool isLocked)
        {
            locked = isLocked;
            if (locked)
            {
                foreach (var group in menuLookup.Values)
                {
                    if (group != null)
                    {
                        group.interactable = false;
                    }
                }
            }
            else
            {
                foreach (var group in menuLookup.Values)
                {
                    if (group != null)
                    {
                        group.interactable = true;
                    }
                }
            }
        }
    }
}




