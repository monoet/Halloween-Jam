using System.Reflection;
using BattleV2.Actions;
using BattleV2.Charge;
using BattleV2.Core;
using BattleV2.Orchestration.Services;
using NUnit.Framework;
using UnityEngine;

public class CombatantActionValidatorTests
{
    private CombatantActionValidator validator;
    private ActionCatalog catalog;
    private GameObject actorObject;

    [SetUp]
    public void SetUp()
    {
        catalog = ScriptableObject.CreateInstance<ActionCatalog>();
        validator = new CombatantActionValidator(catalog);
        actorObject = new GameObject("Actor");
    }

    [TearDown]
    public void TearDown()
    {
        if (actorObject != null)
        {
            Object.DestroyImmediate(actorObject);
        }

        if (catalog != null)
        {
            Object.DestroyImmediate(catalog);
        }
    }

    [Test]
    public void TryValidate_ReturnsTrue_WhenResourcesAndCanExecute()
    {
        var actor = CreateCombatantState(maxCp: 5, currentCp: 3, maxSp: 5, currentSp: 3);
        var context = new CombatContext(actor, null, null, null, new BattleServices(), catalog);
        var actionData = new BattleActionData
        {
            id = "valid",
            actionImpl = CreateActionProvider(costCp: 2, costSp: 1, canExecute: true)
        };

        bool result = validator.TryValidate(actionData, actor, context, cpCharge: 0, out var implementation);

        Assert.IsTrue(result, "Expected validation to succeed with sufficient resources and CanExecute=true.");
        Assert.IsNotNull(implementation, "Implementation should be resolved on success.");
    }

    [Test]
    public void TryValidate_ReturnsFalse_WhenInsufficientCp()
    {
        var actor = CreateCombatantState(maxCp: 1, currentCp: 0, maxSp: 5, currentSp: 5);
        var context = new CombatContext(actor, null, null, null, new BattleServices(), catalog);
        var actionData = new BattleActionData
        {
            id = "requires-cp",
            actionImpl = CreateActionProvider(costCp: 1, costSp: 0, canExecute: true)
        };

        bool result = validator.TryValidate(actionData, actor, context, cpCharge: 0, out var implementation);

        Assert.IsFalse(result, "Validation should fail when actor lacks required CP.");
        Assert.IsNull(implementation, "Implementation should be null on failure.");
    }

    private CombatantState CreateCombatantState(int maxCp, int currentCp, int maxSp, int currentSp)
    {
        var combatant = actorObject.AddComponent<CombatantState>();
        SetPrivateField(combatant, "maxCP", maxCp);
        SetPrivateField(combatant, "currentCP", currentCp);
        SetPrivateField(combatant, "maxSP", maxSp);
        SetPrivateField(combatant, "currentSP", currentSp);
        return combatant;
    }

    private static ScriptableObject CreateActionProvider(int costCp, int costSp, bool canExecute)
    {
        var provider = ScriptableObject.CreateInstance<TestActionProvider>();
        provider.Configure(costCp, costSp, canExecute);
        return provider;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
        field.SetValue(target, value);
    }

    private sealed class TestActionProvider : ScriptableObject, IActionProvider
    {
        private TestAction action;

        public void Configure(int costCp, int costSp, bool canExecute)
        {
            action = new TestAction
            {
                CostCP = costCp,
                CostSP = costSp,
                CanExecuteResult = canExecute,
                ChargeProfile = ChargeProfile.CreateRuntimeDefault()
            };
        }

        public IAction Get() => action;
    }

    private sealed class TestAction : IAction
    {
        public string Id { get; set; } = "test";
        public int CostSP { get; set; }
        public int CostCP { get; set; }
        public ChargeProfile ChargeProfile { get; set; } = ChargeProfile.CreateRuntimeDefault();
        public bool CanExecuteResult { get; set; } = true;

        public bool CanExecute(CombatantState actor, CombatContext context, int cpCharge) => CanExecuteResult;

        public void Execute(CombatantState actor, CombatContext context, int cpCharge, TimedHitResult? timedResult, System.Action onComplete)
        {
            onComplete?.Invoke();
        }
    }
}
