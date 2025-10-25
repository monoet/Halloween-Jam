using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using BattleV2.Actions;
using BattleV2.Core;
using BattleV2.Orchestration.Services;
using BattleV2.Targeting;
using NUnit.Framework;
using UnityEngine;

public class TargetingCoordinatorTests
{
    private TargetingCoordinator coordinator;
    private TargetResolverRegistry registry;
    private BattleEventBus eventBus;
    private GameObject root;

    [SetUp]
    public void SetUp()
    {
        registry = new TargetResolverRegistry();
        eventBus = new BattleEventBus();
        coordinator = new TargetingCoordinator(registry, eventBus);
        root = new GameObject("TestRoot");
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null)
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public async Task ResolveAsync_UsesFallback_WhenResolversReturnNone()
    {
        var player = CreateCombatant("Player", isAlive: true);
        var enemy = CreateCombatant("Enemy", isAlive: true);

        var allies = new List<CombatantState> { player };
        var enemies = new List<CombatantState> { enemy };

        var action = new BattleActionData { id = "basic" };

        var result = await coordinator.ResolveAsync(
            enemy,
            action,
            TargetSourceType.Auto,
            player,
            allies,
            enemies);

        Assert.IsFalse(result.TargetSet.IsEmpty, "Fallback should yield a non-empty target set.");
        Assert.AreEqual(1, result.Targets.Count, "Fallback should provide a single target.");
        Assert.AreSame(player, result.Targets[0], "Fallback target should default to supplied fallback combatant.");
    }

    private CombatantState CreateCombatant(string name, bool isAlive)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform);
        var combatant = go.AddComponent<CombatantState>();
        SetPrivateField(combatant, "maxHP", 10);
        SetPrivateField(combatant, "currentHP", isAlive ? 10 : 0);
        return combatant;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Field '{fieldName}' not found on {target.GetType().Name}.");
        field.SetValue(target, value);
    }
}
