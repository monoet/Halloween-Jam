using System;
using System.Collections.Generic;
using System.Linq;
using BattleV2.Actions;
using BattleV2.Orchestration;
using BattleV2.Providers;
using UnityEngine;

namespace BattleV2.Debugging
{
    /// <summary>
    /// Simple code-only overlay that allows triggering battle actions without the production UI.
    /// </summary>
    public class BattleDebugHarness : MonoBehaviour, IBattleInputProvider
    {
        [Tooltip("Position and size of the debug window.")]
        [SerializeField] private Rect windowRect = new Rect(20f, 20f, 360f, 540f);
        [Tooltip("Maximum number of log entries to retain in memory.")]
        [SerializeField] private int maxLogEntries = 30;

        private BattleManagerV2 manager;
        private BattleActionContext currentContext;
        private Action<BattleSelection> currentOnSelected;
        private Action currentOnCancel;
        private CombatantState cachedPlayer;
        private CombatantState cachedEnemy;
        private Vector2 logScroll;
        private Vector2 listScroll;
        private readonly List<string> logEntries = new();
        private string statusLine = "Harness idle. Awaiting battle start.";
        private bool suppressWindow;
        private GUIStyle boldLabelStyle;
        private GUIStyle headerStyle;

        private void Awake()
        {
            manager = FindObjectOfType<BattleManagerV2>();
            if (manager == null)
            {
                Debug.LogWarning("[BattleDebugHarness] No BattleManagerV2 found in scene. Harness disabled.");
                enabled = false;
                return;
            }

            manager.SetRuntimeInputProvider(this);
            manager.OnPlayerActionSelected += HandlePlayerActionSelected;
            manager.OnPlayerActionResolved += HandlePlayerActionResolved;

            AppendLog("Battle debug harness ready.");
        }

        private void OnDestroy()
        {
            if (manager != null)
            {
                manager.OnPlayerActionSelected -= HandlePlayerActionSelected;
                manager.OnPlayerActionResolved -= HandlePlayerActionResolved;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.BackQuote))
            {
                suppressWindow = !suppressWindow;
            }
        }

        public void RequestAction(BattleActionContext context, Action<BattleSelection> onSelected, Action onCancel)
        {
            currentContext = context;
            currentOnSelected = onSelected;
            currentOnCancel = onCancel;
            cachedPlayer = context?.Player;
            cachedEnemy = context?.Enemy;

            int count = context?.AvailableActions?.Count ?? 0;
            statusLine = $"Awaiting player command ({count} action(s) available).";
            AppendLog(statusLine);
        }

        private void ClearRequest()
        {
            currentContext = null;
            currentOnSelected = null;
            currentOnCancel = null;
        }

        private void OnGUI()
        {
            if (suppressWindow || !enabled)
            {
                return;
            }

            GUI.depth = 0;
            windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindowContents, "Battle Debug Harness");
        }

        private void DrawWindowContents(int windowId)
        {
            EnsureStyles();

            GUILayout.Label(statusLine, GUILayout.ExpandWidth(true));
            DrawVitalsSection();
            GUILayout.Space(6f);
            DrawControls();
            GUILayout.Space(6f);
            DrawLog();

            GUILayout.Space(4f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Start Battle"))
            {
                manager?.SetRuntimeInputProvider(this);
                manager?.StartBattle();
                AppendLog("Battle start requested.");
            }

            if (GUILayout.Button("Reset Battle"))
            {
                manager?.ResetBattle();
                AppendLog("Battle reset requested.");
            }

            if (GUILayout.Button("Hide Harness"))
            {
                suppressWindow = true;
            }
            GUILayout.EndHorizontal();

            GUI.DragWindow(new Rect(0f, 0f, windowRect.width, 20f));
        }

        private void DrawVitalsSection()
        {
            GUILayout.Label("Status:", headerStyle);

            if (cachedPlayer != null)
            {
                GUILayout.Label($"Player HP: {cachedPlayer.CurrentHP}/{cachedPlayer.MaxHP}  SP: {cachedPlayer.CurrentSP}/{cachedPlayer.MaxSP}  CP: {cachedPlayer.CurrentCP}/{cachedPlayer.MaxCP}");
            }
            else
            {
                GUILayout.Label("Player: (unknown)");
            }

            if (cachedEnemy != null)
            {
                GUILayout.Label($"Enemy  HP: {cachedEnemy.CurrentHP}/{cachedEnemy.MaxHP}  SP: {cachedEnemy.CurrentSP}/{cachedEnemy.MaxSP}  CP: {cachedEnemy.CurrentCP}/{cachedEnemy.MaxCP}");
            }
            else
            {
                GUILayout.Label("Enemy: (unknown)");
            }
        }

        private void DrawControls()
        {
            GUILayout.Label("Commands:", headerStyle);

            if (currentContext == null)
            {
                GUILayout.Label("No pending player action.");
                return;
            }

            listScroll = GUILayout.BeginScrollView(listScroll, GUILayout.Height(240f));
            DrawActionGroup("Attack", FilterActions(ActionCategory.Attack));
            DrawActionGroup("Magic", FilterActions(ActionCategory.Magic));
            DrawActionGroup("Item", FilterActions(ActionCategory.Item));

            GUILayout.Space(6f);
            if (currentOnCancel != null && GUILayout.Button("Cancel Turn"))
            {
                AppendLog("Turn cancelled via harness.");
                var cancel = currentOnCancel;
                ClearRequest();
                cancel?.Invoke();
            }
            GUILayout.EndScrollView();
        }

        private enum ActionCategory { Attack, Magic, Item }

        private List<BattleActionData> FilterActions(ActionCategory category)
        {
            var results = new List<BattleActionData>();
            if (currentContext?.AvailableActions == null)
            {
                return results;
            }

            var catalog = currentContext.Context?.Catalog;

            foreach (var action in currentContext.AvailableActions)
            {
                if (action == null)
                {
                    continue;
                }

                bool isMagic = catalog != null && catalog.IsMagic(action);
                bool isItem = catalog != null && catalog.IsItem(action);

                switch (category)
                {
                    case ActionCategory.Magic:
                        if (isMagic)
                        {
                            results.Add(action);
                        }
                        break;
                    case ActionCategory.Item:
                        if (isItem)
                        {
                            results.Add(action);
                        }
                        break;
                    case ActionCategory.Attack:
                        if (!isMagic && !isItem)
                        {
                            results.Add(action);
                        }
                        break;
                }
            }

            return results;
        }

        private void DrawActionGroup(string label, List<BattleActionData> actions)
        {
            GUILayout.Label($"{label} ({actions.Count})", boldLabelStyle);

            if (actions.Count == 0)
            {
                GUILayout.Label("  • none •");
                return;
            }

            foreach (var action in actions)
            {
                var display = !string.IsNullOrEmpty(action.displayName) ? action.displayName : action.id;
                if (GUILayout.Button(display))
                {
                    TriggerAction(action, 0);
                }
            }
        }

        private void TriggerAction(BattleActionData action, int cpCharge)
        {
            if (currentOnSelected == null)
            {
                AppendLog("No callback available to submit action.");
                return;
            }

            var selection = new BattleSelection(action, cpCharge);
            AppendLog($"Submitting action: {action.displayName ?? action.id} (Charge {cpCharge}).");

            var callback = currentOnSelected;
            ClearRequest();
            callback?.Invoke(selection);
        }

        private void DrawLog()
        {
            GUILayout.Label("Log:", headerStyle);
            logScroll = GUILayout.BeginScrollView(logScroll, GUILayout.Height(140f));
            foreach (var entry in logEntries)
            {
                GUILayout.Label(entry);
            }
            GUILayout.EndScrollView();
        }

        private void AppendLog(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            logEntries.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            while (logEntries.Count > Mathf.Max(5, maxLogEntries))
            {
                logEntries.RemoveAt(0);
            }
        }

        private void HandlePlayerActionSelected(BattleSelection selection, int cpBefore)
        {
            var name = selection.Action != null ? selection.Action.displayName ?? selection.Action.id : "(unknown)";
            statusLine = $"Executing {name} (CP before: {cpBefore}, charge: {selection.CpCharge}).";
            AppendLog(statusLine);
        }

        private void HandlePlayerActionResolved(BattleSelection selection, int cpBefore, int cpAfter)
        {
            var name = selection.Action != null ? selection.Action.displayName ?? selection.Action.id : "(unknown)";
            statusLine = $"Action resolved: {name} (CP {cpBefore} → {cpAfter}).";
            AppendLog(statusLine);
        }

        private void EnsureStyles()
        {
            if (boldLabelStyle == null)
            {
                boldLabelStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            }

            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    padding = new RectOffset(0, 0, 6, 2)
                };
            }
        }
    }
}
