namespace AtlasDrop.Core.Learning;

public interface ILearningSettingsService
{
    bool IsEnabled { get; }

    void SetEnabled(bool enabled);
}
