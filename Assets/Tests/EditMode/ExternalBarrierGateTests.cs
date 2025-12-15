using System.Threading;
using System.Threading.Tasks;
using BattleV2.AnimationSystem.Execution.Runtime.Core;
using BattleV2.Diagnostics;
using NUnit.Framework;

public class ExternalBarrierGateTests
{
    [SetUp]
    public void SetUp()
    {
        BattleDebug.CaptureMainThread();
        BattleDebug.SetEnabled("EG", true);
    }

    [TearDown]
    public void TearDown()
    {
        BattleDebug.SetEnabled("EG", false);
    }

    [Test]
    public async Task AwaitScopeAsync_BlocksUntilAllRegisteredTasksComplete()
    {
        var gate = new ExternalBarrierGate();
        gate.BeginScope("G1");

        var tcs1 = new TaskCompletionSource<bool>();
        var tcs2 = new TaskCompletionSource<bool>();

        gate.Register(tcs1.Task, new ResourceKey("Locomotion", 1, 1), "Locomotion", "t1");

        gate.Register(tcs2.Task, new ResourceKey("Locomotion", 1, 1), "Locomotion", "t2");

        var awaitTask = gate.AwaitScopeAsync(CancellationToken.None);

        await Task.Delay(25);
        Assert.IsFalse(awaitTask.IsCompleted, "Gate should block while tasks are pending.");

        tcs1.SetResult(true);
        await Task.Delay(25);
        Assert.IsFalse(awaitTask.IsCompleted, "Gate should still block until all tasks complete.");

        tcs2.SetResult(true);
        await awaitTask;
    }
}
