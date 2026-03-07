using SkyWatch.Contracts.Enums;
using SkyWatch.Core.Configuration;

namespace SkyWatch.Safety;

internal sealed class HysteresisTracker
{
    private readonly DateTimeOffset startupTime;
    private SafetyState? proposedState;
    private DateTimeOffset proposedSince;
    private SafetyState currentState = SafetyState.Initializing;
    private DateTimeOffset? lastTransitionTime;

    public HysteresisTracker(DateTimeOffset startupTime)
    {
        this.startupTime = startupTime;
    }

    public SafetyState CurrentState => currentState;

    public DateTimeOffset? LastTransitionTime => lastTransitionTime;

    public bool IsInWarmup(TimingSettings timing, DateTimeOffset now)
    {
        return (now - startupTime).TotalSeconds < timing.WarmupSeconds;
    }

    public bool ShouldTransition(SafetyState proposed, TimingSettings timing, DateTimeOffset now)
    {
        // During warmup, stay Initializing
        if (IsInWarmup(timing, now))
            return false;

        // If proposed matches current, reset tracking and return false
        if (proposed == currentState)
        {
            proposedState = null;
            return false;
        }

        // If proposed differs from what we're currently tracking, start new tracking
        if (proposedState != proposed)
        {
            proposedState = proposed;
            proposedSince = now;
            return false;
        }

        // Same proposed state as what we're tracking; check if enough time has passed
        double elapsedSeconds = (now - proposedSince).TotalSeconds;

        if (currentState != SafetyState.Unsafe && proposed == SafetyState.Unsafe)
        {
            // Transitioning to unsafe: use short debounce
            return elapsedSeconds >= timing.UnsafeDebounceSeconds;
        }

        if (currentState == SafetyState.Unsafe && proposed == SafetyState.Safe)
        {
            // Transitioning from unsafe to safe: enforce minimum hold time and recovery debounce
            if (lastTransitionTime.HasValue)
            {
                double secondsSinceUnsafe = (now - lastTransitionTime.Value).TotalSeconds;
                if (secondsSinceUnsafe < timing.MinimumUnsafeHoldSeconds)
                    return false;
            }

            return elapsedSeconds >= timing.SafeRecoveryDebounceSeconds;
        }

        // Other transitions (e.g., Initializing -> Safe after warmup)
        return elapsedSeconds >= timing.UnsafeDebounceSeconds;
    }

    public void TransitionTo(SafetyState newState, DateTimeOffset now)
    {
        currentState = newState;
        lastTransitionTime = now;
        proposedState = null;
    }
}
