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
        [System.Serializable]
        private sealed class RecipeTweenDefinition
        {
            public bool enabled = true;
            public string recipeId = "run_up";
            public bool useLocalSpace = true;
            public Vector3 absolutePosition = Vector3.zero;
            public float duration = 0.25f;
            public Ease ease = Ease.OutSine;
            public bool returnToInitialPosition = false;
        }

        [Header("Owner")]
        [SerializeField] private CombatantState owner;

        [Header("Tween Target")] 
        [SerializeField] private Transform tweenTarget;

        [Header("Recipe Tweens")] 
        [SerializeField] private List<RecipeTweenDefinition> tweens = new()
        {
            new RecipeTweenDefinition
            {
                enabled = true,
                recipeId = "run_up",
                useLocalSpace = true,
                absolutePosition = new Vector3(2f, -3f, 0f),
                duration = 0.30f,
                ease = Ease.OutCubic
            },
            new RecipeTweenDefinition
            {
                enabled = true,
                recipeId = "basic_attack",
                useLocalSpace = true,
                duration = 0.30f,
                ease = Ease.OutCubic
            },
            new RecipeTweenDefinition
            {
                enabled = true,
                recipeId = "run_back",
                useLocalSpace = true,
                absolutePosition = Vector3.zero,
                duration = 0.90f,
                ease = Ease.InOutSine,
                returnToInitialPosition = true
            }
        };

        [Header("Wind-Up Settings (solo basic_attack)")] 
        [SerializeField] private float frameRate = 60f;
        [SerializeField] private float forward = 0.10f;
        [SerializeField] private float minorBack = 0.135f;
        [SerializeField] private float recoil = -0.177f;

        private StepScheduler scheduler;
        private Tween activeTween;
        private readonly Dictionary<string, RecipeTweenDefinition> lookup = new();
        // Suprime run_up mientras run_back está activo
        private bool suppressRunUp = false;

        private Vector3 initialLocalPos;
        private bool hasInitialPos;

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

            if (tweenTarget == null && owner != null)
            {
                tweenTarget = owner.transform;
            }

            if (tweenTarget != null && owner != null && !tweenTarget.IsChildOf(owner.transform))
            {
                Debug.LogError("[RecipeTweenObserver] tweenTarget does not belong to the owner actor. Please fix prefab bindings.", this);
            }

            RebuildLookup();
        }

        private void Start()
        {
            if (tweenTarget != null)
            {
                initialLocalPos = tweenTarget.localPosition;
                hasInitialPos = true;
            }
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
                Debug.Log($"TTDebug04 [RUN_UP_TRIGGER] actor={context.Actor?.name ?? "(null)"} action={actionId} animRecipe={recipeOverride}", this);
            }
#endif

            if (recipe == null || tweenTarget == null || !MatchesOwner(context))
                return;

            if (activeTween != null && activeTween.IsActive() && activeTween.IsPlaying())
            {
                Debug.LogWarning($"TTDebug01 [OVERLAP] [{TS()}] actor={owner?.name} newRecipe={recipe.Id} previousTweenActive", this);
            }

            // Bloquea RUN_UP si estamos ejecutando RUN_BACK
            if (suppressRunUp && recipe.Id == "run_up")
            {
                Debug.Log("[RecipeTweenObserver] RUN_UP suprimido (run_back activo)", this);
                return;
            }

            if (!lookup.TryGetValue(recipe.Id, out var def) || def == null || !def.enabled)
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
            }

            Vector3 targetPos = def.returnToInitialPosition && hasInitialPos
                ? initialLocalPos
                : def.absolutePosition;
            activeTween = def.useLocalSpace
                ? tweenTarget.DOLocalMove(targetPos, def.duration).SetEase(def.ease)
                : tweenTarget.DOMove(targetPos, def.duration).SetEase(def.ease);
            Debug.Log($"TTDebug01 [START] [{TS()}] actor={owner?.name} recipe={recipe.Id}", this);
        }

        private void PlayWindup()
        {
            Sequence seq = DOTween.Sequence();
            Vector3 startPos = tweenTarget.localPosition;
            float f(int frames) => frames / frameRate;

            Debug.Log($"TTDebug01 [WINDUP] [{TS()}] actor={owner?.name}", this);
            seq.Append(tweenTarget.DOLocalMoveX(startPos.x + forward, f(6)).SetEase(Ease.OutCubic));
            seq.AppendCallback(() => tweenTarget.localPosition = new Vector3(startPos.x + minorBack, startPos.y, startPos.z));
            seq.AppendInterval(f(1));
            seq.AppendCallback(() => tweenTarget.localPosition = new Vector3(startPos.x + recoil, startPos.y, startPos.z));
            seq.AppendInterval(f(4));

            activeTween = seq;
            seq.Play();
        }

        private void KillTween(bool forceComplete)
        {
            if (activeTween == null) return;
            if (forceComplete) activeTween.Complete();
            else activeTween.Kill();
            activeTween = null;
        }

        private void RebuildLookup()
        {
            lookup.Clear();
            if (tweens == null) return;
            foreach (var def in tweens)
            {
                if (def == null || string.IsNullOrWhiteSpace(def.recipeId)) continue;
                lookup[def.recipeId] = def;
            }
        }

        public void OnRecipeCompleted(RecipeExecutionReport report, StepSchedulerContext context)
        {
            if (report.Recipe != null && report.Recipe.Id == "run_back" && MatchesOwner(context))
            {
                Debug.Log($"TTDebug01 [COMPLETE] [{TS()}] actor={owner?.name} recipe={report.Recipe.Id}", this);
                suppressRunUp = false;
                if (hasInitialPos && tweenTarget != null)
                {
                    tweenTarget.localPosition = initialLocalPos;
                }
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
                Debug.Log($"TTDebug01 [IGNORE] [{TS()}] observerOwner={owner?.name} incomingActor={context.Actor?.name}", this);
                return false;
            }

            return true;
        }
    }
}
