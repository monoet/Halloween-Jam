using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using BattleV2.AnimationSystem.Execution.Runtime;
using BattleV2.AnimationSystem.Runtime;
using BattleV2.Core;

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

        [Header("Debug")]
        [SerializeField] private bool logTweenTargets = true;

        [Header("Recipe Tweens (Strategy Assets)")]
        [SerializeField] private List<CombatTweenStrategy> tweenStrategies = new();

        [Header("Wind-Up Settings (solo basic_attack)")]
        [SerializeField] private float frameRate = 60f;
        [SerializeField] private float forward = 0.10f;
        [SerializeField] private float minorBack = 0.135f;
        [SerializeField] private float recoil = -0.177f;

        private StepScheduler scheduler;
        private Tween activeTween;
        private readonly Dictionary<string, CombatTweenStrategy> lookup = new();
        private readonly List<CombatTweenStrategy> runtimeFallbackStrategies = new();
        // Suprime run_up mientras run_back está activo
        private bool suppressRunUp = false;

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
        }

        private void OnEnable() { RebuildLookup(); Register(); }
        private void OnDisable() { Unregister(); KillTween(true); }

        private void Register()
        {
            var installer = AnimationSystemInstaller.Current;
            scheduler = installer != null ? installer.StepScheduler : null;

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
        }

        public void OnRecipeStarted(ActionRecipe recipe, StepSchedulerContext context)
        {
            if (recipe != null)
                Debug.Log($"[RecipeTweenObserver] RecipeStarted → {recipe.Id}", this);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (recipe != null && recipe.Id == "run_up")
            {
                var selection = context.Request.Selection;
                var actionId = selection.Action?.id ?? "(null)";
                var recipeOverride = selection.AnimationRecipeId ?? "(null)";
#if false
                Debug.Log($"TTDebug04 [RUN_UP_TRIGGER] actor={context.Actor?.name ?? "(null)"} action={actionId} animRecipe={recipeOverride}", this);
#endif
            }
#endif

            if (recipe == null || tweenTarget == null || !MatchesOwner(context))
                return;

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

            if (!lookup.TryGetValue(recipe.Id, out var def) || def == null)
                return;

            KillTween(false);

            if (recipe.Id == "basic_attack")
            {
                PlayWindup();
                return;
            }

            if (recipe.Id == "run_back")
            {
                suppressRunUp = true;

                Vector3 startLocal = tweenTarget.localPosition;
                Vector3 targetLocal = homeLocalPos;
                float dist = Vector3.Distance(startLocal, targetLocal);
                float duration = def.duration;

                Debug.Log($"TTDebug-RUNBACK-START >>> actor={owner.name} startLocal={startLocal} targetLocal={targetLocal} dist={dist:F4} duration={duration:F3} hasActiveTween={(activeTween != null && activeTween.IsActive())}", this);

                DOTween.To(() => 0f, _ => { }, 0f, 0.01f).OnComplete(() =>
                {
                    bool complete = activeTween != null && activeTween.IsComplete();
                    Vector3 afterPos = tweenTarget.localPosition;
                    float finalDist = Vector3.Distance(afterPos, targetLocal);

                    Debug.Log($"TTDebug-RUNBACK-AFTERFRAME >>> complete={complete} afterPos={afterPos} finalDist={finalDist:F4}", this);
                });
            }

            bool useLocal = def.useLocalSpace;
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

            if (recipe.Id == "run_back")
            {
                StartCoroutine(DelayedRunBack(targetPos, def.duration, def.ease));
                return;
            }

            activeTween = useLocal
                ? tweenTarget.DOLocalMove(targetPos, def.duration).SetEase(def.ease)
                : tweenTarget.DOMove(targetPos, def.duration).SetEase(def.ease);
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

        private IEnumerator DelayedRunBack(Vector3 targetPos, float duration, Ease ease)
        {
            yield return null; // wait 1 frame

            var startLocal = tweenTarget != null ? tweenTarget.localPosition : Vector3.zero;
            if (logTweenTargets)
            {
                Debug.Log($"[DelayedRunBack] actor={owner?.name ?? "(null)"} startLocal={startLocal} targetLocal={targetPos} duration={duration}", this);
            }

            activeTween = tweenTarget.DOLocalMove(targetPos, duration).SetEase(ease);
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
                runtimeFallbackStrategies.Add(CreateRuntimeStrategy("run_back", true, Vector3.zero, 0.90f, Ease.InOutSine, true));
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
            if (report.Recipe != null && report.Recipe.Id == "run_back" && MatchesOwner(context) && homeCaptured)
            {
#if false
                Debug.Log($"TTDebug01 [COMPLETE] [{TS()}] actor={owner?.name} recipe={report.Recipe.Id}", this);
#endif
                // Snap solo si no hay tween activo o ya se completó; evita teleports in-flight
                if (tweenTarget != null && (activeTween == null || !activeTween.IsActive() || activeTween.IsComplete()))
                {
                    tweenTarget.localPosition = homeLocalPos;
                    tweenTarget.localRotation = homeLocalRot;
                    tweenTarget.localScale = homeLocalScale;
                }

                suppressRunUp = false;
                return;
            }
        }
        public void OnGroupStarted(ActionStepGroup group, StepSchedulerContext context) { }
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
    }
}
