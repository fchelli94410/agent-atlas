using AtlasDrop.Core.Learning;

namespace AtlasDrop.Infrastructure.Learning;

public sealed class InMemoryLearningSettingsService
    : ILearningSettingsService
{
    public bool IsEnabled { get; private set; } = true;

    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
    }
}
