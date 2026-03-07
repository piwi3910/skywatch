using SkyWatch.Contracts.Enums;
using SkyWatch.Contracts.Interfaces;
using SkyWatch.Contracts.Models;

namespace SkyWatch.Safety;

public sealed class SafetyStateStore : ISafetyStateStore
{
    private readonly object lockObj = new();
    private SafetyEvaluation current;

    public SafetyStateStore()
    {
        current = new SafetyEvaluation
        {
            State = SafetyState.Initializing,
            IsSafe = false,
            Reasons = UnsafeReason.None,
            Confidence = 0,
            Explanation = "System initializing",
            EvaluatedAt = DateTimeOffset.UtcNow,
        };
    }

    public event EventHandler<SafetyEvaluation>? StateChanged;

    public SafetyEvaluation Current
    {
        get
        {
            lock (lockObj)
            {
                return current;
            }
        }
    }

    public void Update(SafetyEvaluation evaluation)
    {
        SafetyEvaluation previous;
        lock (lockObj)
        {
            previous = current;
            current = evaluation;
        }

        if (previous.State != evaluation.State)
        {
            StateChanged?.Invoke(this, evaluation);
        }
    }
}
