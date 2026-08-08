using Xunit;
using AtlasDrop.Core.Learning;
using AtlasDrop.Infrastructure.Learning;

namespace AtlasDrop.Tests.Learning;

public sealed class ConfigurableLearningServiceTests
{
    [Fact]
    public async Task Enabled_learning_records_events()
    {
        var repo = new InMemoryLearningRepository();
        var baseService = new LocalLearningService(repo);
        var settings = new InMemoryLearningSettingsService();

        var service = new ConfigurableLearningService(
            baseService,
            settings);

        await service.RecordAsync(
            LearningEventType.DestinationChosen,
            folderId: "A");

        Assert.Single(await service.GetEventsAsync());
    }

    [Fact]
    public async Task Disabled_learning_does_not_record_new_events()
    {
        var repo = new InMemoryLearningRepository();
        var baseService = new LocalLearningService(repo);
        var settings = new InMemoryLearningSettingsService();
        settings.SetEnabled(false);

        var service = new ConfigurableLearningService(
            baseService,
            settings);

        await service.RecordAsync(
            LearningEventType.DestinationChosen,
            folderId: "A");

        Assert.Empty(await service.GetEventsAsync());
    }

    [Fact]
    public async Task Disabling_learning_does_not_delete_existing_history()
    {
        var repo = new InMemoryLearningRepository();
        var baseService = new LocalLearningService(repo);
        var settings = new InMemoryLearningSettingsService();

        var service = new ConfigurableLearningService(
            baseService,
            settings);

        await service.RecordAsync(
            LearningEventType.DestinationChosen,
            folderId: "A");

        settings.SetEnabled(false);

        Assert.Single(await service.GetEventsAsync());
    }

    [Fact]
    public async Task Clear_removes_existing_learning_even_when_disabled()
    {
        var repo = new InMemoryLearningRepository();
        var baseService = new LocalLearningService(repo);
        var settings = new InMemoryLearningSettingsService();

        var service = new ConfigurableLearningService(
            baseService,
            settings);

        await service.RecordAsync(
            LearningEventType.DestinationChosen,
            folderId: "A");

        settings.SetEnabled(false);

        await service.ClearAsync();

        Assert.Empty(await service.GetEventsAsync());
    }

    [Fact]
    public void Learning_can_be_reenabled()
    {
        var settings = new InMemoryLearningSettingsService();

        settings.SetEnabled(false);
        Assert.False(settings.IsEnabled);

        settings.SetEnabled(true);
        Assert.True(settings.IsEnabled);
    }
}
