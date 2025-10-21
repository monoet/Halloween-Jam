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
        private ChargeProfile timedPracticeChargeProfile;
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
        private bool timedPracticeShouldStop;
        private TimedSequenceMode timedSequenceMode = TimedSequenceMode.Inactive;

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
                ResolveProfiles(action, out var chargeProfile, out var timedProfile);
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
                        StartTimedSequence(action, chargeProfile, timedProfile, currentCharge, TimedSequenceMode.Practice);
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

            ResolveProfiles(action, out var chargeProfile, out var timedProfile);

            if (timedProfile != null)
            {
                StartTimedSequence(action, chargeProfile, timedProfile, cpCharge, TimedSequenceMode.Execute);
                return;
            }

            var selection = new BattleSelection(action, cpCharge, chargeProfile, timedProfile);
            AppendLog($"Submitting action: {action.displayName ?? action.id} (Charge {cpCharge}).");
            SubmitSelection(selection);
        }

        private void SubmitSelection(BattleSelection selection)
        {
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

        private void ResolveProfiles(BattleActionData action, out ChargeProfile chargeProfile, out Ks1TimedHitProfile timedProfile)
        {
            chargeProfile = null;
            timedProfile = null;

            if (currentContext == null || action == null)
            {
                return;
            }

            var catalog = currentContext.Context?.Catalog;
            var impl = catalog != null ? catalog.Resolve(action) : null;

            if (impl != null)
            {
                chargeProfile = impl.ChargeProfile;
                if (impl is ITimedHitAction timedHitAction)
                {
                    timedProfile = timedHitAction.TimedHitProfile;
                }
            }
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

        private void StartTimedSequence(
            BattleActionData action,
            ChargeProfile chargeProfile,
            Ks1TimedHitProfile profile,
            int cpCharge,
            TimedSequenceMode mode)
        {
            if (timedPracticeState == TimedPracticeState.Running || timedPracticeState == TimedPracticeState.WaitingNext)
            {
                AppendLog("Timed hit already in progress.");
                return;
            }

            if (timedSequenceMode == TimedSequenceMode.Execute && mode == TimedSequenceMode.Practice)
            {
                AppendLog("Cannot start practice while a live timed hit is running.");
                return;
            }

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
            timedPracticeChargeProfile = chargeProfile;
            timedPracticeProfile = profile;
            timedPracticeCharge = Mathf.Max(0, cpCharge);
            timedPracticeTier = profile.GetTierForCharge(cpCharge);
            timedPracticeTotalPhases = Mathf.Max(0, timedPracticeTier.Hits);
            timedPracticeCurrentPhase = 0;
            timedPracticePerfectCount = 0;
            timedPracticeGoodCount = 0;
            timedPracticeMissCount = 0;
            timedPracticeLastHitNormalized = -1f;
            timedPracticePhaseResolveTime = float.PositiveInfinity;
            timedPracticeShouldStop = false;
            timedSequenceMode = mode;

            if (timedPracticeTotalPhases <= 0)
            {
                timedPracticeState = TimedPracticeState.Completed;
                if (timedSequenceMode == TimedSequenceMode.Execute)
                {
                    CompleteTimedPractice();
                }
                return;
            }

            timedPracticeState = TimedPracticeState.Running;
            BeginTimedPracticePhase();
            string label = action?.displayName ?? action?.id ?? "(unknown)";
            AppendLog($"{(mode == TimedSequenceMode.Practice ? "Practice" : "Execute")} timed hit for {label} (Charge {cpCharge}).");
        }

        private void ResetTimedPractice()
        {
            timedPracticeState = TimedPracticeState.Inactive;
            timedPracticeAction = null;
            timedPracticeChargeProfile = null;
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
            timedPracticeShouldStop = false;
            timedSequenceMode = TimedSequenceMode.Inactive;
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
                    if (timedPracticeCurrentPhase >= timedPracticeTotalPhases || timedPracticeShouldStop)
                    {
                        CompleteTimedPractice();
                    }
                    else
                    {
                        timedPracticeState = TimedPracticeState.Running;
                        BeginTimedPracticePhase();
                    }
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
                    timedPracticeShouldStop = true;
                    int remaining = Mathf.Max(0, timedPracticeTotalPhases - timedPracticeCurrentPhase);
                    for (int phase = timedPracticeCurrentPhase + 1; phase <= timedPracticeTotalPhases; phase++)
                    {
                        int index = phase - 1;
                        if (index >= timedPracticeOutcomes.Count)
                        {
                            timedPracticeOutcomes.Add(TimedHitPhaseOutcome.Miss);
                        }
                        else
                        {
                            timedPracticeOutcomes[index] = TimedHitPhaseOutcome.Miss;
                        }
                    }
                    timedPracticeMissCount += remaining;
                    timedPracticeCurrentPhase = timedPracticeTotalPhases;
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
            var result = BuildTimedHitResult();

            if (timedSequenceMode == TimedSequenceMode.Execute)
            {
                AppendLog($"Timed hit resolved: Perfect {timedPracticePerfectCount}, Good {timedPracticeGoodCount}, Miss {timedPracticeMissCount}.");
                var selection = new BattleSelection(
                    timedPracticeAction,
                    timedPracticeCharge,
                    timedPracticeChargeProfile,
                    timedPracticeProfile,
                    result);
                AppendLog($"Submitting action: {timedPracticeAction?.displayName ?? timedPracticeAction?.id ?? "(unknown)"} (Charge {timedPracticeCharge}, Hits {result.HitsSucceeded}/{result.TotalHits}).");
                SubmitSelection(selection);
                return;
            }

            AppendLog($"Timed hit practice complete: Perfect {timedPracticePerfectCount}, Good {timedPracticeGoodCount}, Miss {timedPracticeMissCount}.");
        }

        private float CalculateTimedPracticeMultiplier()
        {
            int successCount = timedPracticePerfectCount + timedPracticeGoodCount;
            if (successCount <= 0)
            {
                return 0f;
            }

            float perfectMultiplier = timedPracticeTier.PerfectHitMultiplier > 0f ? timedPracticeTier.PerfectHitMultiplier : 1.5f;
            float successMultiplier = timedPracticeTier.SuccessHitMultiplier > 0f ? timedPracticeTier.SuccessHitMultiplier : 1f;
            float totalContribution = timedPracticePerfectCount * perfectMultiplier
                                   + timedPracticeGoodCount * successMultiplier;
            float averageContribution = totalContribution / successCount;
            float tierMultiplier = timedPracticeTier.DamageMultiplier > 0f ? timedPracticeTier.DamageMultiplier : 1f;
            return averageContribution * tierMultiplier;
        }

        private TimedHitResult BuildTimedHitResult()
        {
            int totalHits = Mathf.Max(0, timedPracticeTotalPhases);
            int hitsSucceeded = Mathf.Clamp(timedPracticePerfectCount + timedPracticeGoodCount, 0, totalHits);
            int refund = Mathf.Clamp(hitsSucceeded, 0, timedPracticeTier.RefundMax);
            float multiplier = CalculateTimedPracticeMultiplier();
            return new TimedHitResult(hitsSucceeded, totalHits, refund, multiplier);
        }

        private void DrawTimedPracticePanel()
        {
            GUILayout.BeginVertical(GUI.skin.box);

            string actionName = timedPracticeAction != null
                ? timedPracticeAction.displayName ?? timedPracticeAction.id
                : "(no action)";

            string panelLabel = timedSequenceMode == TimedSequenceMode.Execute ? "Timed Hit (Live)" : "Timed Hit Practice";
            GUILayout.Label($"{panelLabel}: {actionName}");
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
                    string label;
                    if (outcome == TimedHitPhaseOutcome.Perfect)
                    {
                        label = "P";
                    }
                    else if (outcome == TimedHitPhaseOutcome.Good)
                    {
                        label = "G";
                    }
                    else if (outcome == TimedHitPhaseOutcome.Miss)
                    {
                        label = "X";
                    }
                    else
                    {
                        label = "-";
                    }
                    GUILayout.Box(label, style, GUILayout.Width(24f), GUILayout.Height(24f));
                }
                GUILayout.EndHorizontal();
            }

            if (timedPracticeState == TimedPracticeState.Completed && timedSequenceMode == TimedSequenceMode.Practice)
            {
                GUILayout.Space(6f);
                var summary = BuildTimedHitResult();
                GUILayout.Label($"Perfect {timedPracticePerfectCount} | Good {timedPracticeGoodCount} | Miss {timedPracticeMissCount}");
                GUILayout.Label($"Potential Refund {summary.CpRefund} | Estimated Multiplier {summary.DamageMultiplier:F2}");
            }

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            if (timedPracticeState != TimedPracticeState.Running && timedPracticeState != TimedPracticeState.WaitingNext)
            {
                if (timedSequenceMode == TimedSequenceMode.Practice && timedPracticeProfile != null && GUILayout.Button("Replay"))
                {
                    StartTimedSequence(
                        timedPracticeAction,
                        timedPracticeChargeProfile,
                        timedPracticeProfile,
                        timedPracticeCharge,
                        TimedSequenceMode.Practice);
                }
            }
            bool closeEnabled = timedSequenceMode != TimedSequenceMode.Execute || timedPracticeState == TimedPracticeState.Completed;
            bool previousEnabled = GUI.enabled;
            GUI.enabled = closeEnabled;
            if (GUILayout.Button("Close"))
            {
                ResetTimedPractice();
            }
            GUI.enabled = previousEnabled;
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

        private enum TimedSequenceMode
        {
            Inactive,
            Practice,
            Execute
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
