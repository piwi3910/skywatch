using SkyWatch.Contracts.Enums;
using SkyWatch.Contracts.Models;
using SkyWatch.Safety;

namespace SkyWatch.Tests.Safety;

public sealed class SafetyStateStoreTests
{
    [Fact]
    public void InitialStateIsInitializing()
    {
        var store = new SafetyStateStore();

        Assert.Equal(SafetyState.Initializing, store.Current.State);
    }

    [Fact]
    public void UpdateChangesCurrent()
    {
        var store = new SafetyStateStore();
        var evaluation = CreateEvaluation(SafetyState.Safe);

        store.Update(evaluation);

        Assert.Equal(SafetyState.Safe, store.Current.State);
    }

    [Fact]
    public void StateChangedFiresOnStateChange()
    {
        var store = new SafetyStateStore();
        SafetyEvaluation? received = null;
        store.StateChanged += (_, e) => received = e;

        var evaluation = CreateEvaluation(SafetyState.Safe);
        store.Update(evaluation);

        Assert.NotNull(received);
        Assert.Equal(SafetyState.Safe, received.State);
    }

    [Fact]
    public void StateChangedDoesNotFireOnSameState()
    {
        var store = new SafetyStateStore();

        store.Update(CreateEvaluation(SafetyState.Safe));

        var fired = false;
        store.StateChanged += (_, _) => fired = true;

        store.Update(CreateEvaluation(SafetyState.Safe));

        Assert.False(fired);
    }

    [Fact]
    public void ConcurrentUpdatesDoNotCorruptState()
    {
        var store = new SafetyStateStore();
        var tasks = new List<Task>();

        for (int i = 0; i < 100; i++)
        {
            var state = i % 2 == 0 ? SafetyState.Safe : SafetyState.Unsafe;
            tasks.Add(Task.Run(() => store.Update(CreateEvaluation(state))));
        }

        Task.WaitAll(tasks.ToArray());

        var current = store.Current;
        Assert.True(
            current.State == SafetyState.Safe || current.State == SafetyState.Unsafe,
            $"Expected Safe or Unsafe, got {current.State}");
    }

    private static SafetyEvaluation CreateEvaluation(SafetyState state) => new()
    {
        State = state,
        IsSafe = state == SafetyState.Safe,
        Reasons = UnsafeReason.None,
        Confidence = 0.9,
        Explanation = $"State: {state}",
        EvaluatedAt = DateTimeOffset.UtcNow,
    };
}
