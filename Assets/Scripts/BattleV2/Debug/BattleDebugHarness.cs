using System;
using System.Collections.Generic;
using System.Linq;
using BattleV2.Actions;
using BattleV2.Charge;
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
        private readonly Dictionary<BattleActionData, int> chargeSelections = new();
        [Header("Timed Hit Practice")]
        [SerializeField] private KeyCode timedHitKey = KeyCode.Space;
        [SerializeField, Min(0.05f)] private float practiceFallbackDuration = 1f;

        private TimedPracticeState timedPracticeState = TimedPracticeState.Inactive;
        private BattleActionData timedPracticeAction;
        private Ks1TimedHitProfile timedPracticeProfile;
        private Ks1TimedHitProfile.Tier timedPracticeTier;
        private int timedPracticeCharge;
        private int timedPracticeCurrentPhase;
        private int timedPracticeTotalPhases;
        private int timedPracticePerfectCount;
        private int timedPracticeGoodCount;
        private int timedPracticeMissCount;
        private float timedPracticePhaseStartTime;
        private float timedPracticePhaseResolveTime;
        private float timedPracticeLastHitNormalized = -1f;
        private List<TimedHitPhaseOutcome> timedPracticeOutcomes = new();

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

            UpdateTimedPractice();
        }

        public void RequestAction(BattleActionContext context, Action<BattleSelection> onSelected, Action onCancel)
        {
            currentContext = context;
            currentOnSelected = onSelected;
            currentOnCancel = onCancel;
            cachedPlayer = context?.Player;
            cachedEnemy = context?.Enemy;
            ResetTimedPractice();
            chargeSelections.Clear();

            int count = context?.AvailableActions?.Count ?? 0;
            statusLine = $"Awaiting player command ({count} action(s) available).";
            AppendLog(statusLine);
        }

        private void ClearRequest()
        {
            currentContext = null;
            currentOnSelected = null;
            currentOnCancel = null;
            chargeSelections.Clear();
            ResetTimedPractice();
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
            if (timedPracticeState != TimedPracticeState.Inactive)
            {
                DrawTimedPracticePanel();
                GUILayout.Space(6f);
            }
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
                if (action == null)
                {
                    continue;
                }

                int maxCharge = GetMaxChargeFor(action);
                int currentCharge = GetChargeSelection(action);
                var timedProfile = ResolveTimedHitProfile(action);
                bool hasTimedProfile = timedProfile != null;

                GUILayout.BeginHorizontal();

                if (GUILayout.Button(display, GUILayout.ExpandWidth(true)))
                {
                    TriggerAction(action, currentCharge);
                }

                if (maxCharge > 0)
                {
                    GUILayout.Label($"CP {currentCharge}/{maxCharge}", GUILayout.Width(100f));

                    if (GUILayout.Button("-", GUILayout.Width(24f)))
                    {
                        AdjustChargeSelection(action, -1);
                    }

                    if (GUILayout.Button("+", GUILayout.Width(24f)))
                    {
                        AdjustChargeSelection(action, 1);
                    }
                }
                else
                {
                    GUILayout.Label("CP 0", GUILayout.Width(60f));
                }

                if (hasTimedProfile)
                {
                    if (GUILayout.Button("Practice", GUILayout.Width(70f)))
                    {
                        StartTimedPractice(action, timedProfile, currentCharge);
                    }
                }
                else
                {
                    GUILayout.Label("-", GUILayout.Width(70f));
                }

                GUILayout.EndHorizontal();

                if (maxCharge > 0)
                {
                    float sliderValue = GUILayout.HorizontalSlider(currentCharge, 0f, maxCharge);
                    int sliderCharge = Mathf.RoundToInt(sliderValue);
                    if (sliderCharge != currentCharge)
                    {
                        SetChargeSelection(action, sliderCharge);
                    }
                }

                GUILayout.Space(3f);
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

        private Ks1TimedHitProfile ResolveTimedHitProfile(BattleActionData action)
        {
            if (currentContext == null || action == null)
            {
                return null;
            }

            var catalog = currentContext.Context?.Catalog;
            var impl = catalog != null ? catalog.Resolve(action) : null;
            if (impl is ITimedHitAction timedHitAction)
            {
                return timedHitAction.TimedHitProfile;
            }

            return null;
        }

        private int GetChargeSelection(BattleActionData action)
        {
            if (action == null)
            {
                return 0;
            }

            if (!chargeSelections.TryGetValue(action, out int value))
            {
                value = 0;
                chargeSelections[action] = value;
            }

            int max = GetMaxChargeFor(action);
            if (value > max)
            {
                value = max;
                chargeSelections[action] = value;
            }

            return value;
        }

        private void SetChargeSelection(BattleActionData action, int value)
        {
            if (action == null)
            {
                return;
            }

            int max = GetMaxChargeFor(action);
            chargeSelections[action] = Mathf.Clamp(value, 0, max);
        }

        private void AdjustChargeSelection(BattleActionData action, int delta)
        {
            if (action == null)
            {
                return;
            }

            int current = GetChargeSelection(action);
            SetChargeSelection(action, current + delta);
        }

        private int GetMaxChargeFor(BattleActionData action)
        {
            if (currentContext?.Player == null || action == null)
            {
                return 0;
            }

            int availableCp = Mathf.Max(0, currentContext.Player.CurrentCP);
            int baseCost = Mathf.Max(0, action.costCP);
            return Mathf.Max(0, availableCp - baseCost);
        }

        private void StartTimedPractice(BattleActionData action, Ks1TimedHitProfile profile, int cpCharge)
        {
            if (profile == null)
            {
                AppendLog("Timed hit practice requested without a profile.");
                return;
            }

            if (timedPracticeOutcomes == null)
            {
                timedPracticeOutcomes = new List<TimedHitPhaseOutcome>();
            }

            timedPracticeOutcomes.Clear();
            timedPracticeAction = action;
            timedPracticeProfile = profile;
            timedPracticeCharge = Mathf.Max(0, cpCharge);
            timedPracticeTier = profile.GetTierForCharge(cpCharge);
            timedPracticeTotalPhases = Mathf.Max(1, timedPracticeTier.Hits);
            timedPracticeCurrentPhase = 0;
            timedPracticePerfectCount = 0;
            timedPracticeGoodCount = 0;
            timedPracticeMissCount = 0;
            timedPracticeLastHitNormalized = -1f;
            timedPracticePhaseResolveTime = float.PositiveInfinity;

            if (timedPracticeTotalPhases <= 0)
            {
                timedPracticeState = TimedPracticeState.Completed;
                return;
            }

            timedPracticeState = TimedPracticeState.Running;
            BeginTimedPracticePhase();
            AppendLog($"Timed hit practice started for {action?.displayName ?? action?.id ?? "(unknown)"} (Charge {cpCharge}).");
        }

        private void ResetTimedPractice()
        {
            timedPracticeState = TimedPracticeState.Inactive;
            timedPracticeAction = null;
            timedPracticeProfile = null;
            timedPracticeTier = default;
            timedPracticeCharge = 0;
            timedPracticeCurrentPhase = 0;
            timedPracticeTotalPhases = 0;
            timedPracticePerfectCount = 0;
            timedPracticeGoodCount = 0;
            timedPracticeMissCount = 0;
            timedPracticePhaseStartTime = 0f;
            timedPracticePhaseResolveTime = 0f;
            timedPracticeLastHitNormalized = -1f;
            timedPracticeOutcomes?.Clear();
        }

        private void BeginTimedPracticePhase()
        {
            if (timedPracticeTotalPhases <= 0)
            {
                CompleteTimedPractice();
                return;
            }

            timedPracticeCurrentPhase++;
            if (timedPracticeCurrentPhase > timedPracticeTotalPhases)
            {
                CompleteTimedPractice();
                return;
            }

            timedPracticePhaseStartTime = Time.time;
            timedPracticePhaseResolveTime = float.PositiveInfinity;
            timedPracticeLastHitNormalized = -1f;

            if (timedPracticeCurrentPhase > timedPracticeOutcomes.Count)
            {
                timedPracticeOutcomes.Add(TimedHitPhaseOutcome.Pending);
            }
            else
            {
                timedPracticeOutcomes[timedPracticeCurrentPhase - 1] = TimedHitPhaseOutcome.Pending;
            }
        }

        private void UpdateTimedPractice()
        {
            if (timedPracticeState == TimedPracticeState.Inactive || timedPracticeState == TimedPracticeState.Completed)
            {
                return;
            }

            float duration = Mathf.Max(0.05f, GetCurrentTimelineDuration());

            if (timedPracticeState == TimedPracticeState.Running)
            {
                float elapsed = Time.time - timedPracticePhaseStartTime;
                bool phaseResolved = GetCurrentPhaseOutcome() != TimedHitPhaseOutcome.Pending;

                if (!phaseResolved)
                {
                    if (Input.GetKeyDown(timedHitKey))
                    {
                        float normalizedTime = Mathf.Clamp01(elapsed / duration);
                        ResolveTimedPracticePhase(normalizedTime, false);
                    }
                    else if (elapsed >= duration)
                    {
                        ResolveTimedPracticePhase(1f, true);
                    }
                }
            }
            else if (timedPracticeState == TimedPracticeState.WaitingNext)
            {
                if (Time.time >= timedPracticePhaseResolveTime)
                {
                    timedPracticeState = TimedPracticeState.Running;
                    BeginTimedPracticePhase();
                }
            }
        }

        private void ResolveTimedPracticePhase(float normalizedTime, bool autoMiss)
        {
            var outcome = DetermineTimedOutcome(normalizedTime, autoMiss);
            timedPracticeOutcomes[timedPracticeCurrentPhase - 1] = outcome;
            timedPracticeLastHitNormalized = normalizedTime;

            switch (outcome)
            {
                case TimedHitPhaseOutcome.Perfect:
                    timedPracticePerfectCount++;
                    break;
                case TimedHitPhaseOutcome.Good:
                    timedPracticeGoodCount++;
                    break;
                case TimedHitPhaseOutcome.Miss:
                    timedPracticeMissCount++;
                    break;
            }

            float hold = timedPracticeTier.ResultHoldDuration > 0f ? timedPracticeTier.ResultHoldDuration : 0.35f;
            timedPracticePhaseResolveTime = Time.time + hold;
            timedPracticeState = TimedPracticeState.WaitingNext;
        }

        private TimedHitPhaseOutcome DetermineTimedOutcome(float normalizedTime, bool autoMiss)
        {
            if (autoMiss)
            {
                return TimedHitPhaseOutcome.Miss;
            }

            float center = Mathf.Clamp01(timedPracticeTier.PerfectWindowCenter <= 0f && timedPracticeTier.PerfectWindowRadius <= 0f ? 0.5f : timedPracticeTier.PerfectWindowCenter);
            float perfectRadius = Mathf.Max(0f, timedPracticeTier.PerfectWindowRadius);
            float successRadius = Mathf.Max(perfectRadius, timedPracticeTier.SuccessWindowRadius);
            float delta = Mathf.Abs(normalizedTime - center);

            if (delta <= perfectRadius)
            {
                return TimedHitPhaseOutcome.Perfect;
            }

            if (delta <= successRadius)
            {
                return TimedHitPhaseOutcome.Good;
            }

            return TimedHitPhaseOutcome.Miss;
        }

        private TimedHitPhaseOutcome GetCurrentPhaseOutcome()
        {
            if (timedPracticeCurrentPhase <= 0 || timedPracticeCurrentPhase > timedPracticeOutcomes.Count)
            {
                return TimedHitPhaseOutcome.Pending;
            }

            return timedPracticeOutcomes[timedPracticeCurrentPhase - 1];
        }

        private float GetCurrentTimelineDuration()
        {
            float duration = timedPracticeTier.TimelineDuration;
            if (duration <= 0f)
            {
                duration = practiceFallbackDuration;
            }

            return duration;
        }

        private float GetTimedPracticeProgress()
        {
            if (timedPracticeState == TimedPracticeState.WaitingNext || timedPracticeState == TimedPracticeState.Completed)
            {
                return 1f;
            }

            float duration = Mathf.Max(0.05f, GetCurrentTimelineDuration());
            return Mathf.Clamp01((Time.time - timedPracticePhaseStartTime) / duration);
        }

        private void CompleteTimedPractice()
        {
            timedPracticeState = TimedPracticeState.Completed;
            timedPracticePhaseResolveTime = 0f;
            AppendLog($"Timed hit practice complete: Perfect {timedPracticePerfectCount}, Good {timedPracticeGoodCount}, Miss {timedPracticeMissCount}.");
        }

        private float CalculateTimedPracticeMultiplier()
        {
            if (timedPracticeTotalPhases <= 0)
            {
                return timedPracticeTier.DamageMultiplier > 0f ? timedPracticeTier.DamageMultiplier : 1f;
            }

            float perfectMultiplier = timedPracticeTier.PerfectHitMultiplier > 0f ? timedPracticeTier.PerfectHitMultiplier : 1.5f;
            float successMultiplier = timedPracticeTier.SuccessHitMultiplier > 0f ? timedPracticeTier.SuccessHitMultiplier : 1f;
            float missMultiplier = timedPracticeTier.MissHitMultiplier > 0f ? timedPracticeTier.MissHitMultiplier : 0.5f;

            float total = timedPracticePerfectCount * perfectMultiplier
                        + timedPracticeGoodCount * successMultiplier
                        + timedPracticeMissCount * missMultiplier;

            float average = total / timedPracticeTotalPhases;
            float tierMultiplier = timedPracticeTier.DamageMultiplier > 0f ? timedPracticeTier.DamageMultiplier : 1f;
            return average * tierMultiplier;
        }

        private void DrawTimedPracticePanel()
        {
            GUILayout.BeginVertical(GUI.skin.box);

            string actionName = timedPracticeAction != null
                ? timedPracticeAction.displayName ?? timedPracticeAction.id
                : "(no action)";

            GUILayout.Label($"Timed Hit Practice: {actionName}");
            GUILayout.Label($"Charge {timedPracticeCharge} | Phase {Mathf.Min(timedPracticeCurrentPhase, Math.Max(1, timedPracticeTotalPhases))}/{timedPracticeTotalPhases}");
            GUILayout.Label($"Press {timedHitKey} when the marker enters the highlighted window.");

            Rect barRect = GUILayoutUtility.GetRect(300f, 24f, GUILayout.ExpandWidth(true));
            DrawTimedPracticeBar(barRect);

            GUILayout.Space(4f);

            if (timedPracticeState == TimedPracticeState.Running)
            {
                GUILayout.Label("Awaiting input...");
            }
            else if (timedPracticeState == TimedPracticeState.WaitingNext)
            {
                GUILayout.Label("Next phase incoming...");
            }

            if (timedPracticeOutcomes != null && timedPracticeOutcomes.Count > 0)
            {
                GUILayout.Space(4f);
                GUILayout.BeginHorizontal();
                for (int i = 0; i < timedPracticeTotalPhases; i++)
                {
                    var outcome = i < timedPracticeOutcomes.Count ? timedPracticeOutcomes[i] : TimedHitPhaseOutcome.Pending;
                    GUIStyle style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter };
                    string label = outcome switch
                    {
                        TimedHitPhaseOutcome.Perfect => "P",
                        TimedHitPhaseOutcome.Good => "G",
                        TimedHitPhaseOutcome.Miss => "X",
                        _ => "…"
                    };
                    GUILayout.Box(label, style, GUILayout.Width(24f), GUILayout.Height(24f));
                }
                GUILayout.EndHorizontal();
            }

            if (timedPracticeState == TimedPracticeState.Completed)
            {
                GUILayout.Space(6f);
                int hitsSucceeded = Mathf.Clamp(timedPracticePerfectCount + timedPracticeGoodCount, 0, timedPracticeTotalPhases);
                int refund = Mathf.Clamp(hitsSucceeded, 0, timedPracticeTier.RefundMax);
                float finalMultiplier = CalculateTimedPracticeMultiplier();
                GUILayout.Label($"Perfect {timedPracticePerfectCount} | Good {timedPracticeGoodCount} | Miss {timedPracticeMissCount}");
                GUILayout.Label($"Potential Refund {refund} | Estimated Multiplier {finalMultiplier:F2}");
            }

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            if (timedPracticeState != TimedPracticeState.Running && timedPracticeState != TimedPracticeState.WaitingNext)
            {
                if (timedPracticeProfile != null && GUILayout.Button("Replay"))
                {
                    StartTimedPractice(timedPracticeAction, timedPracticeProfile, timedPracticeCharge);
                }
            }
            if (GUILayout.Button("Close"))
            {
                ResetTimedPractice();
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private void DrawTimedPracticeBar(Rect rect)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            Color previous = GUI.color;

            GUI.color = new Color(0.15f, 0.15f, 0.15f, 1f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            float center = Mathf.Clamp01(timedPracticeTier.PerfectWindowCenter <= 0f && timedPracticeTier.PerfectWindowRadius <= 0f ? 0.5f : timedPracticeTier.PerfectWindowCenter);
            float perfectHalf = Mathf.Max(0f, timedPracticeTier.PerfectWindowRadius) * rect.width;
            float successHalf = Mathf.Max(perfectHalf, timedPracticeTier.SuccessWindowRadius) * rect.width;
            float centerX = rect.x + rect.width * center;

            if (successHalf > 0f)
            {
                GUI.color = new Color(1f, 0.85f, 0.3f, 0.45f);
                GUI.DrawTexture(new Rect(centerX - successHalf, rect.y, successHalf * 2f, rect.height), Texture2D.whiteTexture);
            }

            if (perfectHalf > 0f)
            {
                GUI.color = new Color(0.3f, 1f, 0.35f, 0.65f);
                GUI.DrawTexture(new Rect(centerX - perfectHalf, rect.y, perfectHalf * 2f, rect.height), Texture2D.whiteTexture);
            }

            float progress = GetTimedPracticeProgress();
            GUI.color = Color.white;
            float progressX = rect.x + rect.width * progress;
            GUI.DrawTexture(new Rect(progressX - 1f, rect.y, 2f, rect.height), Texture2D.whiteTexture);

            if (timedPracticeLastHitNormalized >= 0f)
            {
                float hitX = rect.x + rect.width * Mathf.Clamp01(timedPracticeLastHitNormalized);
                GUI.color = new Color(0.9f, 0.2f, 0.2f, 0.8f);
                GUI.DrawTexture(new Rect(hitX - 1f, rect.y, 2f, rect.height), Texture2D.whiteTexture);
            }

            GUI.color = previous;
        }

        private enum TimedPracticeState
        {
            Inactive,
            Running,
            WaitingNext,
            Completed
        }

        private enum TimedHitPhaseOutcome
        {
            Pending,
            Perfect,
            Good,
            Miss
        }
    }
}
