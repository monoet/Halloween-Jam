using DG.Tweening;
using UnityEngine;
using BattleV2.AnimationSystem.Execution.Runtime;
using BattleV2.AnimationSystem.Runtime;

namespace BattleV2.AnimationSystem.Execution.Runtime.Observers
{
    /// <summary>
    /// Observa el StepScheduler y dispara tweens DOTween independientes cuando
    /// se detectan recipes específicos (por defecto basic_attack y run_up).
    /// </summary>
    public sealed class RecipeTweenObserver : MonoBehaviour, IStepSchedulerObserver
    {
        [System.Serializable]
        private class RecipeTweenSettings
        {
            [Tooltip("Recipe Id que disparará este tween.")]
            public string recipeId;

            [Tooltip("Posición local absoluta a la que se moverá el root.")]
            public Vector3 targetLocalPosition = new Vector3(3f, -2f, 0f);

            [Tooltip("Duración del desplazamiento.")]
            public float duration = 0.2f;

            [Tooltip("Curva de easing usada por DOTween.")]
            public Ease ease = Ease.OutQuad;
        }

        [Header("Tween Target")]
        [SerializeField] private Transform tweenTarget;

        [Header("Recipes")]
        [SerializeField] private RecipeTweenSettings basicAttackSettings = new RecipeTweenSettings
        {
            recipeId = "basic_attack",
            targetLocalPosition = new Vector3(3f, -2f, 0f),
            duration = 0.2f,
            ease = Ease.OutQuad
        };

        [SerializeField] private RecipeTweenSettings runUpSettings = new RecipeTweenSettings
        {
            recipeId = "run_up",
            targetLocalPosition = new Vector3(1.5f, -0.5f, 0f),
            duration = 0.25f,
            ease = Ease.OutSine
        };

        private StepScheduler scheduler;
        private Tween activeTween;

        private void OnEnable()
        {
            Register();
        }

        private void OnDisable()
        {
            Unregister();
            KillTween(forceComplete: true);
        }

        private void Register()
        {
            var installer = AnimationSystemInstaller.Current;
            scheduler = installer != null ? installer.StepScheduler : null;

            if (scheduler == null)
            {
                Debug.LogWarning("[RecipeTweenObserver] StepScheduler no disponible; deshabilitando el observer.", this);
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
            if (recipe == null || tweenTarget == null)
            {
                return;
            }

            var settings = ResolveSettings(recipe.Id);
            if (settings == null)
            {
                return;
            }

            KillTween(forceComplete: false);
            activeTween = tweenTarget.DOLocalMove(settings.targetLocalPosition, settings.duration)
                .SetEase(settings.ease);
        }

        private RecipeTweenSettings ResolveSettings(string recipeId)
        {
            if (string.IsNullOrWhiteSpace(recipeId))
            {
                return null;
            }

            if (basicAttackSettings != null && recipeId == basicAttackSettings.recipeId)
            {
                return basicAttackSettings;
            }

            if (runUpSettings != null && recipeId == runUpSettings.recipeId)
            {
                return runUpSettings;
            }

            return null;
        }

        private void KillTween(bool forceComplete)
        {
            if (activeTween == null)
            {
                return;
            }

            if (forceComplete)
            {
                activeTween.Complete();
            }
            else
            {
                activeTween.Kill();
            }

            activeTween = null;
        }

        #region IStepSchedulerObserver - no ops
        public void OnRecipeCompleted(RecipeExecutionReport report, StepSchedulerContext context) { }
        public void OnGroupStarted(ActionStepGroup group, StepSchedulerContext context) { }
        public void OnGroupCompleted(StepGroupExecutionReport report, StepSchedulerContext context) { }
        public void OnBranchTaken(string sourceId, string targetId, StepSchedulerContext context) { }
        public void OnStepStarted(ActionStep step, StepSchedulerContext context) { }
        public void OnStepCompleted(StepExecutionReport report, StepSchedulerContext context) { }
        #endregion
    }
}
