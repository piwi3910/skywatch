using SkyWatch.Contracts.Models;

namespace SkyWatch.Contracts.Interfaces;

public interface ISafetyStateStore
{
    event EventHandler<SafetyEvaluation>? StateChanged;

    SafetyEvaluation Current { get; }

    void Update(SafetyEvaluation evaluation);
}
