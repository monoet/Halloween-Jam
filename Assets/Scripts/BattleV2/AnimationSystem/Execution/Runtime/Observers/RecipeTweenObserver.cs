using System;
using System;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using BattleV2.AnimationSystem.Execution.Runtime;
using BattleV2.AnimationSystem.Execution.Runtime.Core;
using BattleV2.AnimationSystem.Motion;
using BattleV2.AnimationSystem.Runtime;
using BattleV2.Core;
using BattleV2.Diagnostics;
using BattleV2.Orchestration.Runtime;
using BattleV2.Providers;

namespace BattleV2.AnimationSystem.Execution.Runtime.Observers
{
    public sealed class RecipeTweenObserver : MonoBehaviour, IStepSchedulerObserver
    {
        [Header("Owner")]
        [SerializeField] private CombatantState owner;

        [Header("Tween Target (MotionRoot)")]
        [SerializeField] private Transform tweenTarget;
[Header("Anchors")]
[SerializeField] private Transform globalSpotlight;
[SerializeField] private Transform motionAnchor;

        [Header("Debug")]
        [SerializeField] private bool logTweenTargets = true;

        [Header("Recipe Tweens (Strategy Assets)")]
        [SerializeField] private List<CombatTweenStrategy> tweenStrategies = new();
        [Header("Override Durations (Testing)")]
        [SerializeField] private bool overrideRunUpDuration = false;
        [SerializeField] private float runUpDuration = 0.30f;
        [SerializeField] private bool overrideRunBackDuration = false;
        [SerializeField] private float runBackDuration = 0.90f;

        [Header("Wind-Up Settings (solo basic_attack)")]
        [SerializeField] private float frameRate = 60f;
        [SerializeField] private float forward = 0.10f;
        [SerializeField] private float minorBack = 0.135f;
        [SerializeField] private float recoil = -0.177f;
        [Tooltip("A/B switch: disables windup DOTween to isolate locomotion overlap issues.")]
        [SerializeField] private bool disableWindupTween = false;

        private StepScheduler scheduler;
        private Tween activeTween;
        private MotionService motionService;
        private ResourceKey locomotionKey;
        private IMainThreadInvoker mainThreadInvoker;
        private readonly Dictionary<string, CombatTweenStrategy> lookup = new();
        private readonly List<CombatTweenStrategy> runtimeFallbackStrategies = new();
// Suprime run_up mientras run_back está activo
private bool suppressRunUp = false;
private string lastVariantResetKey;
private Animator cachedAnimator;
private bool? originalRootMotion;
private bool anchorMarkedThisTurn;

        // Home snapshot
        private Vector3 homeLocalPos;
        private Quaternion homeLocalRot;
        private Vector3 homeLocalScale;
        private bool homeCaptured;

        private static string TS()
        {
            float t = Time.realtimeSinceStartup;
            int minutes = (int)(t / 60f);
            float seconds = t % 60f;
            return $"{minutes:00}:{seconds:00.000}";
        }

        private void Awake()
        {
            if (owner == null)
            {
                owner = GetComponentInParent<CombatantState>();
                if (owner == null)
                {
                    Debug.LogWarning("[RecipeTweenObserver] No CombatantState found in parents. Disabling observer.", this);
                    enabled = false;
                    return;
                }
            }

            if (tweenTarget == null)
            {
                Debug.LogError($"[RecipeTweenObserver] Missing MotionRoot/tweenTarget on {name}. Assign it explicitly.", this);
                enabled = false;
                return;
            }

            if (tweenTarget != null && owner != null && !tweenTarget.IsChildOf(owner.transform))
            {
                Debug.LogError("[RecipeTweenObserver] tweenTarget does not belong to the owner actor. Please fix prefab bindings.", this);
            }

            if (globalSpotlight == null)
            {
                var spot = GameObject.Find("SpotlightDestination")
                          ?? GameObject.Find("spotlightDestination")
                          ?? GameObject.Find("spotlightdestination");
                if (spot != null)
                {
                    globalSpotlight = spot.transform;
                }
                else
                {
                    Debug.LogWarning("[RecipeTweenObserver] No globalSpotlight assigned and SpotlightDestination not found in scene.", this);
                }
            }

            RebuildLookup();
            CaptureHomePosition(force: true);
        }

        private void OnEnable() { RebuildLookup(); Register(); }
        private void OnDisable() { Unregister(); KillTween(true); RestoreRootMotion(); anchorMarkedThisTurn = false; lastVariantResetKey = null; }

        private void Register()
        {
            var installer = AnimationSystemInstaller.Current;
            scheduler = installer != null ? installer.StepScheduler : null;
            motionService = installer != null ? installer.MotionService : null;
            mainThreadInvoker = MainThreadInvoker.Instance;
            if (motionService != null)
            {
                locomotionKey = motionService.BuildLocomotionKey(owner, tweenTarget);
                motionService.EnsureHomeSnapshot(locomotionKey, tweenTarget);
            }

            if (scheduler == null)
            {
                Debug.LogWarning("[RecipeTweenObserver] StepScheduler no disponible.", this);
                enabled = false;
                return;
            }
            scheduler.RegisterObserver(this);
        }

        private void Unregister()
        {
            scheduler?.UnregisterObserver(this);
            scheduler = null;
            motionService = null;
            mainThreadInvoker = null;
        }

        public void OnRecipeStarted(ActionRecipe recipe, StepSchedulerContext context)
        {
            if (recipe != null)
                Debug.Log($"[RecipeTweenObserver] RecipeStarted → {recipe.Id}", this);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (recipe != null && recipe.Id == "run_up")
            {
                var dbgSelection = context.Request.Selection;
                var dbgActionId = dbgSelection.Action?.id ?? "(null)";
                var dbgRecipeOverride = dbgSelection.AnimationRecipeId ?? "(null)";
#if false
                Debug.Log($"TTDebug04 [RUN_UP_TRIGGER] actor={context.Actor?.name ?? "(null)"} action={dbgActionId} animRecipe={dbgRecipeOverride}", this);
#endif
            }
#endif

            if (recipe == null || tweenTarget == null || !MatchesOwner(context))
                return;

            var selection = context.Request.Selection;
            var actionId = selection.Action != null ? selection.Action.id : "(null)";
            var recipeOverride = selection.AnimationRecipeId ?? "(null)";
            // Prefer the wrapper instance actually used by the scheduler for this recipe execution.
            BattleV2.Orchestration.Runtime.AnimatorWrapper aw = context.Wrapper as BattleV2.Orchestration.Runtime.AnimatorWrapper;
            if (aw == null)
            {
                if (AnimatorRegistry.Instance.TryGetWrapper(owner, out var registeredWrapper))
                {
                    aw = registeredWrapper as BattleV2.Orchestration.Runtime.AnimatorWrapper;
                }
            }

            if (aw != null)
            {
                var resetKey = $"{owner.GetInstanceID()}|{actionId}|{recipeOverride}|{context.Request.GetHashCode()}";
                if (!string.Equals(resetKey, lastVariantResetKey, StringComparison.Ordinal))
                {
                    lastVariantResetKey = resetKey;
                    aw.ResetVariantScope("ActionBoundary", resetKey);
                }

                var ctx = $"recipe={recipe.Id} action={actionId} override={recipeOverride}";
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                aw.DebugReceiveCommand(recipe.Id, "RecipeTweenObserver", ctx);
#endif

                switch (recipe.Id)
                {
                    case "run_up":
                    case "run_back":
                    case "basic_attack":
                    case "move_to_target":
                        _ = aw.ConsumeCommand(recipe.Id, "RecipeTweenObserver", ctx, System.Threading.CancellationToken.None);
                        break;
                    case "run_up_target":
                        // Opcional: solo comando si quieres consumir anim/clip asociado
                        _ = aw.ConsumeCommand(recipe.Id, "RecipeTweenObserver", ctx, System.Threading.CancellationToken.None);
                        break;
                    case "idle":
                        aw.RequestIdleLoop($"RecipeTweenObserver {ctx}");
                        break;
                }
            }

            if (activeTween != null && activeTween.IsActive() && activeTween.IsPlaying())
            {
#if false
                Debug.LogWarning($"TTDebug01 [OVERLAP] [{TS()}] actor={owner?.name} newRecipe={recipe.Id} previousTweenActive", this);
#endif
            }

            // Bloquea RUN_UP si estamos ejecutando RUN_BACK
            if (suppressRunUp && recipe.Id == "run_up")
            {
                Debug.Log("[RecipeTweenObserver] RUN_UP suprimido (run_back activo)", this);
                return;
            }

            if (string.Equals(recipe.Id, "move_to_target", StringComparison.OrdinalIgnoreCase))
            {
                var targetTransform = ResolveTargetTransform(selection, context.Request.Targets);
                if (targetTransform == null)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning($"[RecipeTweenObserver] move_to_target skipped: target transform not found for actor={owner?.name ?? "(null)"} action={selection.Action?.id ?? "(null)"}", this);
#endif
                    return;
                }

                var spotlight = FindSpotlightDestination(targetTransform);
                var worldTarget = spotlight != null ? spotlight.position : targetTransform.position;
                var moveToTargetPos = tweenTarget.parent != null
                    ? tweenTarget.parent.InverseTransformPoint(worldTarget)
                    : worldTarget;

                if (!lookup.TryGetValue("run_up", out var runUpDef) || runUpDef == null)
                {
                    return;
                }

                SetRootMotion(false);

                if (motionService != null)
                {
                    // Route A: locomotion is fired in OnGroupStarted so ExternalBarrierGate has the correct group scope.
                    return;
                }

                KillTween(false);
                float duration = overrideRunUpDuration ? runUpDuration : runUpDef.duration;
                activeTween = tweenTarget.DOLocalMove(moveToTargetPos, duration).SetEase(Ease.OutCubic);
                return;
            }

            if (!lookup.TryGetValue(recipe.Id, out var def) || def == null)
                return;

            // Locomotion tweens are handled via MotionService (Route A).

            if (recipe.Id == "basic_attack")
            {
                KillTween(false);
                if (disableWindupTween)
                {
                    if (BattleDebug.IsEnabled("RTO"))
                    {
                        BattleDebug.Log("RTO", 40, "basic_attack windup skipped (disableWindupTween=true).", this);
                    }
                    return;
                }
                PlayWindup();
                return;
            }

            if (recipe.Id == "run_up_target")
            {
                if (BattleDebug.IsEnabled("RTO"))
                {
                    BattleDebug.Warn("RTO", 2, "run_up_target skipped (waiting for Chunk 4.5 RecipeChain / multi-recipes).", this);
                }
                return;
            }

            if (recipe.Id == "run_back")
            {
                if (motionService != null)
                {
                    // Route A: locomotion is fired in OnGroupStarted so ExternalBarrierGate has the correct group scope.
                    return;
                }
                if (!homeCaptured)
                {
                    CaptureHomePosition(force: true);
                    if (!homeCaptured)
                    {
                        Debug.LogWarning("[RecipeTweenObserver] RUN_BACK aborted: home position not captured.", this);
                        return;
                    }
                }
                suppressRunUp = true;
                SetRootMotion(false);
                float duration = overrideRunBackDuration ? runBackDuration : def.duration;
                bool allowAnchor = anchorMarkedThisTurn &&
                                   motionAnchor != null &&
                                   string.Equals(context.Request.Selection.Action?.id, "basic_attack", StringComparison.OrdinalIgnoreCase);
                Vector3 startLocal = allowAnchor ? motionAnchor.localPosition : tweenTarget.localPosition;
                tweenTarget.localPosition = startLocal;
                anchorMarkedThisTurn = false;

                // Tween run_back siempre local, ease dramático
                activeTween = tweenTarget
                    .DOLocalMove(homeLocalPos, duration)
                    .SetEase(Ease.OutExpo);
                return;
            }

            // For motion we always tween in local space to avoid world/local drift
            bool useLocal = true;
            Vector3 targetPos = def.returnToInitialPosition && homeCaptured
                ? homeLocalPos
                : def.target;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"TTDebug13 [TWEEN/RESOLVE_BASE] actor={owner?.name ?? "(null)"} recipe={recipe.Id} useLocal={useLocal} defTarget={def.target} homeCaptured={homeCaptured} home={homeLocalPos}", this);
#endif

            if (recipe.Id == "run_up" && globalSpotlight != null)
            {
                var worldTarget = globalSpotlight.position;
                if (tweenTarget.parent != null)
                {
                    targetPos = tweenTarget.parent.InverseTransformPoint(worldTarget);
                    useLocal = true;
                }
                else
                {
                    targetPos = worldTarget;
                    useLocal = false;
                }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"TTDebug13 [RUN_UP/SPOT] actor={owner?.name ?? "(null)"} worldTarget={worldTarget} parent={(tweenTarget.parent != null ? tweenTarget.parent.name : "(null)")}", this);
#endif
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"TTDebug13 [TWEEN/FINAL] actor={owner?.name ?? "(null)"} recipe={recipe.Id} useLocal={useLocal} targetPos={targetPos} startLocal={tweenTarget.localPosition} startWorld={tweenTarget.position}", this);
#endif

            LogTweenTarget(def, targetPos);

            if (recipe.Id == "run_up")
            {
                SetRootMotion(false);
            }

            if (motionService != null)
            {
                return;
            }

            float durationOverride = recipe.Id == "run_up" && overrideRunUpDuration
                ? runUpDuration
                : def.duration;
            activeTween = tweenTarget.DOLocalMove(targetPos, durationOverride).SetEase(def.ease);
        }

        private void PlayWindup()
        {
            Sequence seq = DOTween.Sequence();
            Vector3 startPos = tweenTarget.localPosition;
            float f(int frames) => frames / frameRate;

#if false
            Debug.Log($"TTDebug01 [WINDUP] [{TS()}] actor={owner?.name}", this);
#endif
            seq.Append(tweenTarget.DOLocalMoveX(startPos.x + forward, f(6)).SetEase(Ease.OutCubic));
            seq.AppendCallback(() => tweenTarget.localPosition = new Vector3(startPos.x + minorBack, startPos.y, startPos.z));
            seq.AppendInterval(f(1));
            seq.AppendCallback(() => tweenTarget.localPosition = new Vector3(startPos.x + recoil, startPos.y, startPos.z));
            seq.AppendInterval(f(4));

            activeTween = seq;
            seq.Play();
        }

        private void PlayRunUpTarget(BattleSelection selection, IReadOnlyList<CombatantState> resolvedTargets)
        {
            var targetTransform = ResolveTargetTransform(selection, resolvedTargets);
            if (targetTransform == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning($"[RecipeTweenObserver] run_up_target skipped: target transform not found for actor={owner?.name ?? "(null)"} action={selection.Action?.id ?? "(null)"}", this);
#endif
                return;
            }

            var spotlight = FindSpotlightDestination(targetTransform);
            var worldTarget = spotlight != null ? spotlight.position : targetTransform.position;
            var targetPos = tweenTarget.parent != null
                ? tweenTarget.parent.InverseTransformPoint(worldTarget)
                : worldTarget;

            float duration = overrideRunUpDuration ? runUpDuration : (lookup.TryGetValue("run_up", out var def) && def != null ? def.duration : runUpDuration);
            activeTween = tweenTarget.DOLocalMove(targetPos, duration).SetEase(Ease.OutCubic);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (logTweenTargets)
            {
                Debug.Log($"TTDebug13 [RUN_UP_TARGET] actor={owner?.name ?? "(null)"} target={(spotlight != null ? spotlight.name : targetTransform.name)} world={worldTarget} local={targetPos} duration={duration}", this);
            }
#endif
        }

        private Transform ResolveTargetTransform(BattleSelection selection, IReadOnlyList<CombatantState> resolvedTargets)
        {
            if (selection.TargetTransform != null)
            {
                return selection.TargetTransform;
            }

            if (selection.Targets.HasValue && selection.Targets.Value.TryGetSingle(out var targetId) && resolvedTargets != null)
            {
                var match = resolvedTargets.FirstOrDefault(t => t != null && t.GetInstanceID() == targetId);
                if (match != null)
                {
                    return match.transform;
                }
            }

            if (resolvedTargets != null && resolvedTargets.Count > 0 && resolvedTargets[0] != null)
            {
                return resolvedTargets[0].transform;
            }

            return null;
        }

        private Transform FindSpotlightDestination(Transform root)
        {
            if (root == null)
            {
                return null;
            }

            var direct = root.Find("SpotlightDestination");
            if (direct != null)
            {
                return direct;
            }

            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "SpotlightDestination") ?? root;
        }

        private void KillTween(bool forceComplete)
        {
            if (activeTween == null) return;
            if (forceComplete) activeTween.Complete();
            else activeTween.Kill();
            activeTween = null;
        }

        private void LogTweenTarget(CombatTweenStrategy def, Vector3 targetPos)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!logTweenTargets || tweenTarget == null || owner == null)
            {
                return;
            }

            var startLocal = tweenTarget.localPosition;
            var startWorld = tweenTarget.position;
            Vector3 targetLocal = def.useLocalSpace
                ? targetPos
                : tweenTarget.InverseTransformPoint(targetPos);
            Vector3 targetWorld = def.useLocalSpace
                ? (tweenTarget.parent != null ? tweenTarget.parent.TransformPoint(targetPos) : targetPos)
                : targetPos;
            Debug.Log(
                $"TTDebug12 [TWEEN_TARGET] [{TS()}] actor={owner.name} recipe={def.recipeId} localSpace={def.useLocalSpace} returnInit={def.returnToInitialPosition} startLocal={startLocal} startWorld={startWorld} targetLocal={targetLocal} targetWorld={targetWorld}",
                this);
#endif
        }

        private void RebuildLookup()
        {
            lookup.Clear();
            var strategies = GetStrategies();
            if (strategies == null)
            {
                return;
            }

            foreach (var strategy in strategies)
            {
                if (strategy == null || !strategy.enabled || string.IsNullOrWhiteSpace(strategy.recipeId))
                {
                    continue;
                }

                lookup[strategy.recipeId] = strategy;
            }
        }

        private IEnumerable<CombatTweenStrategy> GetStrategies()
        {
            if (tweenStrategies != null && tweenStrategies.Count > 0)
            {
                return tweenStrategies;
            }

            if (runtimeFallbackStrategies.Count == 0)
            {
                runtimeFallbackStrategies.Add(CreateRuntimeStrategy("run_up", true, new Vector3(2f, -3f, 0f), 0.30f, Ease.OutCubic, false));
                runtimeFallbackStrategies.Add(CreateRuntimeStrategy("basic_attack", true, Vector3.zero, 0.30f, Ease.OutCubic, false));
                runtimeFallbackStrategies.Add(CreateRuntimeStrategy("run_back", true, Vector3.zero, 0.90f, Ease.OutExpo, true));
            }

            return runtimeFallbackStrategies;
        }

        private static CombatTweenStrategy CreateRuntimeStrategy(string recipeId, bool useLocal, Vector3 target, float duration, Ease ease, bool returnInit)
        {
            var strategy = ScriptableObject.CreateInstance<CombatTweenStrategy>();
            strategy.hideFlags = HideFlags.HideAndDontSave;
            strategy.recipeId = recipeId;
            strategy.useLocalSpace = useLocal;
            strategy.target = target;
            strategy.duration = duration;
            strategy.ease = ease;
            strategy.returnToInitialPosition = returnInit;
            return strategy;
        }

        public void OnRecipeCompleted(RecipeExecutionReport report, StepSchedulerContext context)
        {
            if (report.Recipe == null || !MatchesOwner(context))
            {
                return;
            }

            var recipeId = report.Recipe.Id;

            if (recipeId == "basic_attack_windup" && motionAnchor != null && MatchesOwner(context))
            {
                RunOnMainThread(() =>
                {
                    motionAnchor.localPosition = tweenTarget.localPosition;
                    anchorMarkedThisTurn = true;
                });
            }

            if (recipeId == "run_back" && homeCaptured)
            {
                if (motionService != null)
                {
                    // When using MotionService (Route A), avoid snapping based on activeTween (it won't represent locomotion).
                    return;
                }
#if false
                Debug.Log($"TTDebug01 [COMPLETE] [{TS()}] actor={owner?.name} recipe={report.Recipe.Id}", this);
#endif
                // Snap solo si no hay tween activo o ya se completó; evita teleports in-flight
                if (tweenTarget != null && (activeTween == null || !activeTween.IsActive() || activeTween.IsComplete()))
                {
                    RunOnMainThread(() =>
                    {
                        tweenTarget.localPosition = homeLocalPos;
                        tweenTarget.localRotation = homeLocalRot;
                        tweenTarget.localScale = homeLocalScale;
                    });
                }

                suppressRunUp = false;
                RestoreRootMotion();
                return;
            }

            if (recipeId == "run_back" || recipeId == "run_up")
            {
                RestoreRootMotion();
            }
        }
        public void OnGroupStarted(ActionStepGroup group, StepSchedulerContext context)
        {
            if (group == null || tweenTarget == null || !MatchesOwner(context))
            {
                return;
            }

            // Route A: fire locomotion here so ExternalBarrierGate has the correct group scope.
            // If MotionService is null, locomotion remains handled by legacy OnRecipeStarted code.
            if (motionService != null && !string.IsNullOrWhiteSpace(group.Id))
            {
                if (string.Equals(group.Id, "run_up", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(group.Id, "move_to_spotlight", StringComparison.OrdinalIgnoreCase))
                {
                    // Chunk 4.5: when move_to_target is injected before basic_attack, we must not run the
                    // basic_attack "run_up" group (spotlight) or it will yank the actor away from the target.
                    if (string.Equals(context.Request.Selection.Action?.id, "basic_attack", StringComparison.OrdinalIgnoreCase))
                    {
                        if (BattleDebug.IsEnabled("RTO"))
                        {
                            BattleDebug.Log("RTO", 41, "Skipped run_up (spotlight) during basic_attack (spotlight is turn-staging, not attack-staging).", this);
                        }
                        return;
                    }

                    if (!lookup.TryGetValue("run_up", out var def) || def == null)
                    {
                        return;
                    }

                    var targetPos = def.target;
                    if (globalSpotlight != null)
                    {
                        targetPos = motionService.WorldToLocal(tweenTarget, globalSpotlight.position);
                    }

                    SetRootMotion(false);
                    var reason = string.Equals(group.Id, "move_to_spotlight", StringComparison.OrdinalIgnoreCase)
                        ? "move_to_spotlight"
                        : "run_up";
                    context.Gate?.ExpectBarrier("Locomotion", reason);
                    var task = RunUpAsync(def, group.Id, targetPos);
                    context.Gate?.Register(task, locomotionKey, "Locomotion", reason, cancel: () =>
                    {
                        RunOnMainThread(() =>
                        {
                            if (BattleV2.Core.BattleDiagnostics.DevLocomotionTrace)
                            {
                                BattleV2.Core.BattleDiagnostics.Log(
                                    "LOCOMOTIONTRACE",
                                    $"TWEEN_CANCEL actor={owner?.name ?? "(null)"} recipeId={group.Id} reason={reason} key={locomotionKey}",
                                    context: null);
                            }
                            motionService.Cancel(locomotionKey, reason: $"gate_cancel:{reason}");
                        });
                    });
                    return;
                }

                if (string.Equals(group.Id, "move_to_target", StringComparison.OrdinalIgnoreCase))
                {
                    var selection = context.Request.Selection;
                    var targetTransform = ResolveTargetTransform(selection, context.Request.Targets);
                    if (targetTransform == null)
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.LogWarning($"[RecipeTweenObserver] move_to_target skipped: target transform not found for actor={owner?.name ?? "(null)"} action={selection.Action?.id ?? "(null)"}", this);
#endif
                        return;
                    }

                    var spotlight = FindSpotlightDestination(targetTransform);
                    var worldTarget = spotlight != null ? spotlight.position : targetTransform.position;
                    var targetPos = motionService.WorldToLocal(tweenTarget, worldTarget);

                    if (!lookup.TryGetValue("run_up", out var def) || def == null)
                    {
                        return;
                    }

                    SetRootMotion(false);
                    context.Gate?.ExpectBarrier("Locomotion", "move_to_target");
                    var task = RunUpAsync(def, "move_to_target", targetPos);
                    context.Gate?.Register(task, locomotionKey, "Locomotion", "move_to_target", cancel: () =>
                    {
                        RunOnMainThread(() =>
                        {
                            if (BattleV2.Core.BattleDiagnostics.DevLocomotionTrace)
                            {
                                BattleV2.Core.BattleDiagnostics.Log(
                                    "LOCOMOTIONTRACE",
                                    $"TWEEN_CANCEL actor={owner?.name ?? "(null)"} recipeId=move_to_target reason=move_to_target key={locomotionKey}",
                                    context: null);
                            }
                            motionService.Cancel(locomotionKey, reason: "gate_cancel:move_to_target");
                        });
                    });
                    return;
                }

                if (string.Equals(group.Id, "run_back", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(group.Id, "return_home", StringComparison.OrdinalIgnoreCase))
                {
                    if (!lookup.TryGetValue("run_back", out var def) || def == null)
                    {
                        return;
                    }

                    var reason = string.Equals(group.Id, "return_home", StringComparison.OrdinalIgnoreCase)
                        ? "return_home"
                        : "run_back";
                    context.Gate?.ExpectBarrier("Locomotion", reason);
                    var task = RunBackAsync(def, context);
                    context.Gate?.Register(task, locomotionKey, "Locomotion", reason, cancel: () =>
                    {
                        RunOnMainThread(() =>
                        {
                            if (BattleV2.Core.BattleDiagnostics.DevLocomotionTrace)
                            {
                                BattleV2.Core.BattleDiagnostics.Log(
                                    "LOCOMOTIONTRACE",
                                    $"TWEEN_CANCEL actor={owner?.name ?? "(null)"} recipeId={group.Id} reason={reason} key={locomotionKey}",
                                    context: null);
                            }
                            motionService.Cancel(locomotionKey, reason: $"gate_cancel:{reason}");
                        });
                    });
                    return;
                }
            }

            // Permite disparar el acercamiento al objetivo cuando el GroupId es run_up_target (dentro de basic_attack)
            if (string.Equals(group.Id, "run_up_target", StringComparison.OrdinalIgnoreCase))
            {
                if (BattleDebug.IsEnabled("RTO"))
                {
                    BattleDebug.Warn("RTO", 30, "run_up_target skipped (waiting for Chunk 4.5 RecipeChain / multi-recipes).");
                }
                return;
            }
        }

        public void OnGroupCompleted(StepGroupExecutionReport report, StepSchedulerContext context) { }
        public void OnBranchTaken(string sourceId, string targetId, StepSchedulerContext context) { }
        public void OnStepStarted(ActionStep step, StepSchedulerContext context) { }
        public void OnStepCompleted(StepExecutionReport report, StepSchedulerContext context) { }

        private bool MatchesOwner(StepSchedulerContext context)
        {
            if (owner == null || context.Actor == null)
            {
                return false;
            }

            if (!ReferenceEquals(context.Actor, owner))
            {
                return false;
            }

            return true;
        }

        public void CaptureHomePosition(bool force = false)
        {
            if (tweenTarget == null)
            {
                return;
            }

            if (homeCaptured && !force)
            {
                return;
            }

            homeLocalPos = tweenTarget.localPosition;
            homeLocalRot = tweenTarget.localRotation;
            homeLocalScale = tweenTarget.localScale;
            homeCaptured = true;
        }

        public void ResetToHomeImmediate()
        {
            if (!homeCaptured || tweenTarget == null)
            {
                return;
            }

            tweenTarget.localPosition = homeLocalPos;
            tweenTarget.localRotation = homeLocalRot;
            tweenTarget.localScale = homeLocalScale;
        }

        private async System.Threading.Tasks.Task RunUpAsync(CombatTweenStrategy def, string recipeId, Vector3 targetLocalPos)
        {
            if (motionService == null || tweenTarget == null)
            {
                return;
            }

            float durationOverride = recipeId == "run_up" && overrideRunUpDuration
                ? runUpDuration
                : def.duration;

            try
            {
                LocomotionTrace(() =>
                    $"TWEEN_START actor={owner?.name ?? "(null)"} recipeId={recipeId} key={locomotionKey} to={targetLocalPos} duration={durationOverride:0.###} ease={def.ease}");

                await motionService.MoveToLocalAsync(
                        locomotionKey,
                        tweenTarget,
                        targetLocalPos,
                        durationOverride,
                        def.ease,
                        StepConflictPolicy.CancelRunning,
                        System.Threading.CancellationToken.None,
                        reason: recipeId)
                    .ConfigureAwait(false);

                LocomotionTrace(() =>
                    $"TWEEN_COMPLETE actor={owner?.name ?? "(null)"} recipeId={recipeId} key={locomotionKey}");
            }
            catch (OperationCanceledException)
            {
                LocomotionTrace(() =>
                    $"TWEEN_CANCEL actor={owner?.name ?? "(null)"} recipeId={recipeId} key={locomotionKey}");
            }
            catch
            {
                LocomotionTrace(() =>
                    $"TWEEN_FAIL actor={owner?.name ?? "(null)"} recipeId={recipeId} key={locomotionKey}");
            }
        }

        private async System.Threading.Tasks.Task RunBackAsync(CombatTweenStrategy def, StepSchedulerContext context)
        {
            if (motionService == null || tweenTarget == null)
            {
                return;
            }

            suppressRunUp = true;
            SetRootMotion(false);

            float duration = overrideRunBackDuration ? runBackDuration : def.duration;
            bool allowAnchor = anchorMarkedThisTurn &&
                               motionAnchor != null &&
                               string.Equals(context.Request.Selection.Action?.id, "basic_attack", StringComparison.OrdinalIgnoreCase);
            Vector3? overrideStart = allowAnchor ? motionAnchor.localPosition : null;
            anchorMarkedThisTurn = false;

            try
            {
                LocomotionTrace(() =>
                    $"TWEEN_START actor={owner?.name ?? "(null)"} recipeId=run_back key={locomotionKey} duration={duration:0.###} ease={Ease.OutExpo}");

                await motionService.ReturnHomeAsync(
                         locomotionKey,
                         tweenTarget,
                         duration,
                         Ease.OutExpo,
                         StepConflictPolicy.CancelRunning,
                         System.Threading.CancellationToken.None,
                         overrideStartLocalPos: overrideStart,
                         reason: "run_back")
                     .ConfigureAwait(false);

                LocomotionTrace(() =>
                    $"TWEEN_COMPLETE actor={owner?.name ?? "(null)"} recipeId=run_back key={locomotionKey}");

                if (context.Wrapper is BattleV2.Orchestration.Runtime.AnimatorWrapper aw)
                {
                    BattleV2.AnimationSystem.Runtime.IdleEnsureUtility.EnsureIdleNextTick(
                        aw,
                        "RecipeTweenObserver.RunBackComplete");
                }
            }
            catch (OperationCanceledException)
            {
                LocomotionTrace(() =>
                    $"TWEEN_CANCEL actor={owner?.name ?? "(null)"} recipeId=run_back key={locomotionKey}");
            }
            catch
            {
                LocomotionTrace(() =>
                    $"TWEEN_FAIL actor={owner?.name ?? "(null)"} recipeId=run_back key={locomotionKey}");
            }
            finally
            {
                suppressRunUp = false;
                RestoreRootMotion();
            }
        }

        private async System.Threading.Tasks.Task RunUpTargetAsync(BattleSelection selection, IReadOnlyList<CombatantState> resolvedTargets)
        {
            if (motionService == null || tweenTarget == null)
            {
                return;
            }

            var targetTransform = ResolveTargetTransform(selection, resolvedTargets);
            if (targetTransform == null)
            {
                return;
            }

            var spotlight = FindSpotlightDestination(targetTransform);
            var worldTarget = spotlight != null ? spotlight.position : targetTransform.position;
            var targetPos = motionService.WorldToLocal(tweenTarget, worldTarget);

            float duration = overrideRunUpDuration
                ? runUpDuration
                : (lookup.TryGetValue("run_up", out var def) && def != null ? def.duration : runUpDuration);

            try
            {
                LocomotionTrace(() =>
                    $"TWEEN_START actor={owner?.name ?? "(null)"} recipeId=run_up_target key={locomotionKey} to={targetPos} duration={duration:0.###} ease={Ease.OutCubic}");

                await motionService.MoveToLocalAsync(
                        locomotionKey,
                        tweenTarget,
                        targetPos,
                        duration,
                        Ease.OutCubic,
                        StepConflictPolicy.CancelRunning,
                        System.Threading.CancellationToken.None,
                        reason: "run_up_target")
                    .ConfigureAwait(false);

                LocomotionTrace(() =>
                    $"TWEEN_COMPLETE actor={owner?.name ?? "(null)"} recipeId=run_up_target key={locomotionKey}");
            }
            catch (OperationCanceledException)
            {
                LocomotionTrace(() =>
                    $"TWEEN_CANCEL actor={owner?.name ?? "(null)"} recipeId=run_up_target key={locomotionKey}");
            }
            catch
            {
                LocomotionTrace(() =>
                    $"TWEEN_FAIL actor={owner?.name ?? "(null)"} recipeId=run_up_target key={locomotionKey}");
            }
        }

        private Animator GetAnimator()
        {
            if (cachedAnimator != null)
            {
                return cachedAnimator;
            }

            if (owner != null)
            {
                cachedAnimator = owner.GetComponentInChildren<Animator>(true);
            }

            return cachedAnimator;
        }

        private void LocomotionTrace(Func<string> buildMessage)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!BattleV2.Core.BattleDiagnostics.DevLocomotionTrace || buildMessage == null)
            {
                return;
            }

            RunOnMainThread(() =>
            {
                BattleV2.Core.BattleDiagnostics.Log("LOCOMOTIONTRACE", buildMessage(), context: null);
            });
#endif
        }

        private void RunOnMainThread(Action action)
        {
            if (action == null)
            {
                return;
            }

            if (BattleDebug.IsMainThread)
            {
                action();
                return;
            }

            (mainThreadInvoker ?? MainThreadInvoker.Instance).Run(action);
        }

        private void SetRootMotion(bool enabled)
        {
            RunOnMainThread(() =>
            {
                var animator = GetAnimator();
                if (animator == null)
                {
                    return;
                }

                originalRootMotion ??= animator.applyRootMotion;
                animator.applyRootMotion = enabled;
            });
        }

        private void RestoreRootMotion()
        {
            RunOnMainThread(() =>
            {
                var animator = GetAnimator();
                if (animator == null || !originalRootMotion.HasValue)
                {
                    return;
                }

                animator.applyRootMotion = originalRootMotion.Value;
            });
        }

    }
}
