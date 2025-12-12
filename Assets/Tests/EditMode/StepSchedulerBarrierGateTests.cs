using System;
using System.Threading;
using System.Threading.Tasks;
using BattleV2.Actions;
using BattleV2.AnimationSystem;
using BattleV2.AnimationSystem.Execution.Runtime;
using BattleV2.AnimationSystem.Execution.Runtime.Core;
using BattleV2.AnimationSystem.Execution.Runtime.Executors;
using BattleV2.Providers;
using NUnit.Framework;

public class StepSchedulerBarrierGateTests
{
    [Test]
    public async Task StepScheduler_DoesNotEnterNextGroup_UntilExternalBarriersComplete()
    {
        var scheduler = new StepScheduler();
        scheduler.RegisterExecutor(new WaitExecutor());

        var stepA = new ActionStep(WaitExecutor.ExecutorId, bindingId: null, parameters: default, id: "stepA");
        var stepB = new ActionStep(WaitExecutor.ExecutorId, bindingId: null, parameters: default, id: "stepB");

        var groupA = new ActionStepGroup("A", new[] { stepA }, StepGroupExecutionMode.Sequential);
        var groupB = new ActionStepGroup("B", new[] { stepB }, StepGroupExecutionMode.Sequential);
        var recipe = new ActionRecipe("test", new[] { groupA, groupB });

        var selection = new BattleSelection(new BattleActionData { id = "test" }, animationRecipeId: "test");
        var request = new AnimationRequest(actor: null, selection, targets: Array.Empty<CombatantState>(), averageSpeed: 1f, recipeOverride: null);

        var gate = new ExternalBarrierGate();
        var context = new StepSchedulerContext(
            request,
            timeline: null,
            wrapper: null,
            bindingResolver: null,
            routerBundle: null,
            eventBus: null,
            timedHitService: null,
            skipResetToFallback: true,
            gate: gate);

        DateTime? barrierCompletedAt = null;
        DateTime? enteredBAt = null;

        scheduler.RegisterObserver(new TestBarrierObserver(
            gate,
            onBarrierCompleted: t => barrierCompletedAt = t,
            onEnteredB: t => enteredBAt = t));

        await scheduler.ExecuteAsync(recipe, context, CancellationToken.None);

        Assert.IsTrue(barrierCompletedAt.HasValue, "Barrier completion timestamp missing.");
        Assert.IsTrue(enteredBAt.HasValue, "Group B entry timestamp missing.");
        Assert.GreaterOrEqual(enteredBAt.Value, barrierCompletedAt.Value, "Group B started before barrier completion.");
    }

    private sealed class TestBarrierObserver : IStepSchedulerObserver
    {
        private readonly ExternalBarrierGate gate;
        private readonly Action<DateTime> onBarrierCompleted;
        private readonly Action<DateTime> onEnteredB;

        public TestBarrierObserver(ExternalBarrierGate gate, Action<DateTime> onBarrierCompleted, Action<DateTime> onEnteredB)
        {
            this.gate = gate;
            this.onBarrierCompleted = onBarrierCompleted;
            this.onEnteredB = onEnteredB;
        }

        public void OnRecipeStarted(ActionRecipe recipe, StepSchedulerContext context) { }

        public void OnGroupStarted(ActionStepGroup group, StepSchedulerContext context)
        {
            if (string.Equals(group.Id, "A", StringComparison.OrdinalIgnoreCase))
            {
                var delayTask = Task.Run(async () =>
                {
                    await Task.Delay(200);
                    onBarrierCompleted(DateTime.UtcNow);
                });

                gate.Register(delayTask, new ResourceKey("Locomotion", 1, 1), "Locomotion", "test");
            }
            else if (string.Equals(group.Id, "B", StringComparison.OrdinalIgnoreCase))
            {
                onEnteredB(DateTime.UtcNow);
            }
        }

        public void OnGroupCompleted(StepGroupExecutionReport report, StepSchedulerContext context) { }
        public void OnBranchTaken(string sourceId, string targetId, StepSchedulerContext context) { }
        public void OnStepStarted(ActionStep step, StepSchedulerContext context) { }
        public void OnStepCompleted(StepExecutionReport report, StepSchedulerContext context) { }
        public void OnRecipeCompleted(RecipeExecutionReport report, StepSchedulerContext context) { }
    }
}

