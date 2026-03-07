using SkyWatch.Contracts.Enums;
using SkyWatch.Core.Configuration;
using SkyWatch.Safety;

namespace SkyWatch.Tests.Safety;

public sealed class HysteresisTrackerTests
{
    private static readonly DateTimeOffset StartTime = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static TimingSettings DefaultTiming => new()
    {
        WarmupSeconds = 30,
        UnsafeDebounceSeconds = 10,
        SafeRecoveryDebounceSeconds = 300,
        MinimumUnsafeHoldSeconds = 60,
    };

    [Fact]
    public void DuringWarmupShouldTransitionReturnsFalse()
    {
        var tracker = new HysteresisTracker(StartTime);
        var now = StartTime.AddSeconds(15);

        var result = tracker.ShouldTransition(SafetyState.Safe, DefaultTiming, now);

        Assert.False(result);
    }

    [Fact]
    public void AfterWarmupSafeToUnsafeRespectsUnsafeDebounceSeconds()
    {
        var tracker = new HysteresisTracker(StartTime);
        var timing = DefaultTiming;

        var afterWarmup = StartTime.AddSeconds(31);
        tracker.TransitionTo(SafetyState.Safe, afterWarmup);

        var t1 = afterWarmup.AddSeconds(1);
        Assert.False(tracker.ShouldTransition(SafetyState.Unsafe, timing, t1));

        var t2 = t1.AddSeconds(9);
        Assert.False(tracker.ShouldTransition(SafetyState.Unsafe, timing, t2));

        var t3 = t1.AddSeconds(10);
        Assert.True(tracker.ShouldTransition(SafetyState.Unsafe, timing, t3));
    }

    [Fact]
    public void UnsafeToSafeRespectsSafeRecoveryDebounceSeconds()
    {
        var tracker = new HysteresisTracker(StartTime);
        var timing = DefaultTiming;

        var afterWarmup = StartTime.AddSeconds(31);
        tracker.TransitionTo(SafetyState.Unsafe, afterWarmup);

        var t1 = afterWarmup.AddSeconds(timing.MinimumUnsafeHoldSeconds + 1);
        Assert.False(tracker.ShouldTransition(SafetyState.Safe, timing, t1));

        var t2 = t1.AddSeconds(299);
        Assert.False(tracker.ShouldTransition(SafetyState.Safe, timing, t2));

        var t3 = t1.AddSeconds(300);
        Assert.True(tracker.ShouldTransition(SafetyState.Safe, timing, t3));
    }

    [Fact]
    public void MinimumUnsafeHoldSecondsPreventsEarlySafeRecovery()
    {
        var tracker = new HysteresisTracker(StartTime);
        var timing = DefaultTiming;

        var afterWarmup = StartTime.AddSeconds(31);
        tracker.TransitionTo(SafetyState.Unsafe, afterWarmup);

        // Only 10s into unsafe hold (need 60s minimum)
        var t1 = afterWarmup.AddSeconds(10);
        Assert.False(tracker.ShouldTransition(SafetyState.Safe, timing, t1));

        // Wait long enough to satisfy both minimum hold and recovery debounce
        var t2 = t1.AddSeconds(400);
        Assert.True(tracker.ShouldTransition(SafetyState.Safe, timing, t2));
    }

    [Fact]
    public void ChangingProposedStateResetsTimer()
    {
        var tracker = new HysteresisTracker(StartTime);
        var timing = DefaultTiming;

        var afterWarmup = StartTime.AddSeconds(31);
        tracker.TransitionTo(SafetyState.Safe, afterWarmup);

        var t1 = afterWarmup.AddSeconds(1);
        tracker.ShouldTransition(SafetyState.Unsafe, timing, t1);

        var t2 = t1.AddSeconds(9);
        Assert.False(tracker.ShouldTransition(SafetyState.Unsafe, timing, t2));

        // Propose current state (Safe) to reset tracking
        var t3 = t2.AddSeconds(1);
        tracker.ShouldTransition(SafetyState.Safe, timing, t3);

        // Propose Unsafe again; timer should have reset
        var t4 = t3.AddSeconds(1);
        Assert.False(tracker.ShouldTransition(SafetyState.Unsafe, timing, t4));

        var t5 = t4.AddSeconds(10);
        Assert.True(tracker.ShouldTransition(SafetyState.Unsafe, timing, t5));
    }

    [Fact]
    public void SameProposedStateAsCurrentReturnsFalse()
    {
        var tracker = new HysteresisTracker(StartTime);
        var timing = DefaultTiming;

        var afterWarmup = StartTime.AddSeconds(31);
        tracker.TransitionTo(SafetyState.Safe, afterWarmup);

        var t1 = afterWarmup.AddSeconds(100);
        Assert.False(tracker.ShouldTransition(SafetyState.Safe, timing, t1));
    }
}
