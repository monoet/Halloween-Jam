using BattleV2.AnimationSystem;
using BattleV2.AnimationSystem.Execution.Runtime;
using BattleV2.Charge;
using BattleV2.Execution.TimedHits;
using UnityEngine;

namespace BattleV2.AnimationSystem.Runtime
{
    /// <summary>
    /// Reacts to per-phase KS1 outcomes and queues lightweight animation recipes.
    /// </summary>
    public sealed class Ks1PhaseAnimationBridge : MonoBehaviour
    {
        [SerializeField] private AnimationSystemInstaller installer;
        [SerializeField] private Ks1TimedHitRunner runner;

        [Header("Recipe Ids")]
        [SerializeField] private string goodPhaseRecipeId;
        [SerializeField] private string perfectPhaseRecipeId;
        [SerializeField] private string missPhaseRecipeId;
        [SerializeField] private string chainCompletedRecipeId;
        [SerializeField] private string chainCancelledRecipeId;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs;

        private bool runnerSubscribed;
        private bool terminalRecipePlayed;

        private void Awake()
        {
            installer ??= AnimationSystemInstaller.Current;
            runner ??= GetComponent<Ks1TimedHitRunner>();
            TrySubscribe();
        }

        private void OnEnable()
        {
            TrySubscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void TrySubscribe()
        {
            if (runnerSubscribed && runner != null && runner.isActiveAndEnabled)
            {
                return;
            }

            Unsubscribe();

            runner ??= GetComponent<Ks1TimedHitRunner>();

            if (runner == null)
            {
                installer ??= AnimationSystemInstaller.Current;
                runner = installer?.TimedHitService?.GetRunner(TimedHitRunnerKind.Default) as Ks1TimedHitRunner;
            }

            if (runner != null && runner.isActiveAndEnabled)
            {
                runner.OnSequenceStarted += HandleSequenceStarted;
                runner.OnSequenceCompleted += HandleSequenceCompleted;
                runner.PhaseResolved += HandlePhaseResolved;
                runnerSubscribed = true;
                terminalRecipePlayed = false;
            }
        }

        private void Unsubscribe()
        {
            if (runner != null && runnerSubscribed)
            {
                runner.OnSequenceStarted -= HandleSequenceStarted;
                runner.OnSequenceCompleted -= HandleSequenceCompleted;
                runner.PhaseResolved -= HandlePhaseResolved;
            }

            runnerSubscribed = false;
            terminalRecipePlayed = false;
        }

        private void Update()
        {
            if (!runnerSubscribed)
            {
                TrySubscribe();
                return;
            }

            if (runner == null || !runner.isActiveAndEnabled)
            {
                Unsubscribe();
            }
        }

        private void HandleSequenceStarted()
        {
            terminalRecipePlayed = false;
        }

        private void HandleSequenceCompleted(TimedHitResult result)
        {
            terminalRecipePlayed = true;
        }

        private void HandlePhaseResolved(Ks1PhaseOutcome outcome)
        {
            var actor = outcome.Actor;
            if (actor == null)
            {
                return;
            }

            bool isTerminalOutcome = outcome.ChainCancelled || outcome.ChainCompleted;
            if (terminalRecipePlayed && !isTerminalOutcome)
            {
                return;
            }

            var recipeId = ResolveRecipe(outcome);
            if (string.IsNullOrWhiteSpace(recipeId))
            {
                return;
            }

            if (isTerminalOutcome)
            {
                terminalRecipePlayed = true;
            }

            if (enableDebugLogs)
            {
                Debug.Log(
                    $"[KS1 ANIM] Phase {outcome.PhaseIndex + 1}/{outcome.TotalPhases}: {outcome.Judgment} (cancelled={outcome.ChainCancelled}, completed={outcome.ChainCompleted})",
                    this);
            }

            var orchestrator = installer?.Orchestrator;
            if (orchestrator == null)
            {
                return;
            }

            var context = AnimationContext.Default.WithPrimaryActor(actor);
            _ = orchestrator.PlayRecipeAsync(recipeId, context);
        }

        private string ResolveRecipe(Ks1PhaseOutcome outcome)
        {
            if (outcome.ChainCancelled)
            {
                return chainCancelledRecipeId ?? string.Empty;
            }

            if (outcome.ChainCompleted)
            {
                return chainCompletedRecipeId ?? string.Empty;
            }

            return outcome.Judgment switch
            {
                TimedHitJudgment.Perfect when !string.IsNullOrEmpty(perfectPhaseRecipeId) => perfectPhaseRecipeId,
                TimedHitJudgment.Good when !string.IsNullOrEmpty(goodPhaseRecipeId) => goodPhaseRecipeId,
                _ => missPhaseRecipeId
            };
        }
    }
}
