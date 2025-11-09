using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using BattleV2.AnimationSystem.Execution.Runtime;
using BattleV2.AnimationSystem.Runtime;

namespace BattleV2.AnimationSystem.Execution.Runtime.Observers
{
    /// <summary>
    /// Ejecuta tweens DOTween independientes cuando determinados recipes (basic_attack, run_up, etc.)
    /// son lanzados por el StepScheduler. 
    /// 'basic_attack' usa una animación frameada para reproducir el wind-up con precisión.
    /// </summary>
    public sealed class RecipeTweenObserver : MonoBehaviour, IStepSchedulerObserver
    {
        [System.Serializable]
        private sealed class RecipeTweenDefinition
        {
            [Tooltip("Habilita o deshabilita el tween sin borrar la entrada.")]
            public bool enabled = true;

            [Tooltip("ID exacto del recipe que dispara este tween.")]
            public string recipeId = "run_up";

            [Tooltip("Usar espacio local (true) o world (false).")]
            public bool useLocalSpace = true;

            [Tooltip("Posición absoluta (si aplica).")]
            public Vector3 absolutePosition = Vector3.zero;

            [Tooltip("Duración total (solo para tweens simples).")]
            public float duration = 0.25f;

            [Tooltip("Curva de easing usada por DOTween.")]
            public Ease ease = Ease.OutSine;
        }

        [Header("Tween Target")]
        [SerializeField] private Transform tweenTarget;

        [Header("Recipe Tweens")]
        [SerializeField] private List<RecipeTweenDefinition> tweens = new()
        {
            // Run-Up → desplazamiento absoluto
            new RecipeTweenDefinition
            {
                enabled = true,
                recipeId = "run_up",
                useLocalSpace = true,
                absolutePosition = new Vector3(2f, -3f, 0f),
                duration = 0.30f,
                ease = Ease.OutCubic
            },
            // Basic Attack → wind-up frameado
            new RecipeTweenDefinition
            {
                enabled = true,
                recipeId = "basic_attack",
                useLocalSpace = true,
                duration = 0.30f,
                ease = Ease.OutCubic
            }
        };

        [Header("Wind-Up Settings (solo basic_attack)")]
        [SerializeField, Tooltip("Fotogramas por segundo usados para el cálculo de tiempos.")]
        private float frameRate = 60f;

        [SerializeField, Tooltip("Avance inicial del wind-up (frame 0–6).")]
        private float forward = 0.10f;

        [SerializeField, Tooltip("Pequeño retroceso en el frame 6→7.")]
        private float minorBack = 0.135f;

        [SerializeField, Tooltip("Retroceso brusco del golpe (frame 7→8).")]
        private float recoil = -0.177f;

        private StepScheduler scheduler;
        private Tween activeTween;
        private readonly Dictionary<string, RecipeTweenDefinition> lookup = new();

        private void Awake() => RebuildLookup();
        private void OnEnable() { RebuildLookup(); Register(); }
        private void OnDisable() { Unregister(); KillTween(forceComplete: true); }

        private void Register()
        {
            var installer = AnimationSystemInstaller.Current;
            scheduler = installer != null ? installer.StepScheduler : null;

            if (scheduler == null)
            {
                Debug.LogWarning("[RecipeTweenObserver] StepScheduler no disponible. Observer deshabilitado.", this);
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
                return;

            if (!lookup.TryGetValue(recipe.Id, out var def) || def == null || !def.enabled)
                return;

            KillTween(false);

            // --- Wind-Up por frames ---
            if (recipe.Id == "basic_attack")
            {
                PlayWindup();
                return;
            }

            // --- Run-Up u otros simples ---
            Vector3 targetPos = def.absolutePosition;
            activeTween = def.useLocalSpace
                ? tweenTarget.DOLocalMove(targetPos, def.duration).SetEase(def.ease)
                : tweenTarget.DOMove(targetPos, def.duration).SetEase(def.ease);
        }

private void PlayWindup()
{
    Sequence seq = DOTween.Sequence();
    Vector3 startPos = tweenTarget.localPosition;

    float f(int frames) => frames / frameRate;

    // Frame 0 → 6 : avanza con ease out
    seq.Append(tweenTarget.DOLocalMoveX(startPos.x + forward, f(6))
        .SetEase(Ease.OutCubic));

    // Frame 6 → 7 : salto inmediato a +0.135
    seq.AppendCallback(() =>
        tweenTarget.localPosition = new Vector3(startPos.x + minorBack, startPos.y, startPos.z));

    // Frame 7 → 8 : golpe brusco a -0.177
    seq.AppendInterval(f(1));
    seq.AppendCallback(() =>
        tweenTarget.localPosition = new Vector3(startPos.x + recoil, startPos.y, startPos.z));

    // Mantener hasta frame 11 (ahora se queda aquí)
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

        #region IStepSchedulerObserver (no-op)
        public void OnRecipeCompleted(RecipeExecutionReport report, StepSchedulerContext context) { }
        public void OnGroupStarted(ActionStepGroup group, StepSchedulerContext context) { }
        public void OnGroupCompleted(StepGroupExecutionReport report, StepSchedulerContext context) { }
        public void OnBranchTaken(string sourceId, string targetId, StepSchedulerContext context) { }
        public void OnStepStarted(ActionStep step, StepSchedulerContext context) { }
        public void OnStepCompleted(StepExecutionReport report, StepSchedulerContext context) { }
        #endregion
    }
}
