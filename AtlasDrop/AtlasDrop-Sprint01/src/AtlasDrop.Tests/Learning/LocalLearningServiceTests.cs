using Xunit;
using AtlasDrop.Core.Learning;
using AtlasDrop.Infrastructure.Learning;

namespace AtlasDrop.Tests.Learning;

public sealed class LocalLearningServiceTests
{
    [Theory]
    [InlineData(LearningEventType.DestinationChosen)]
    [InlineData(LearningEventType.DestinationRejected)]
    [InlineData(LearningEventType.ManualSearch)]
    [InlineData(LearningEventType.FolderCreated)]
    [InlineData(LearningEventType.RenameCorrected)]
    [InlineData(LearningEventType.OperationUndone)]
    public async Task Supported_event_types_are_recorded(
        LearningEventType type)
    {
        var repo = new InMemoryLearningRepository();
        var service = new LocalLearningService(repo);

        await service.RecordAsync(
            type,
            folderId: "folder-1",
            contextKey: "invoice-edf",
            value: "signal");

        var events = await service.GetEventsAsync();

        var item = Assert.Single(events);
        Assert.Equal(type, item.Type);
        Assert.Equal("folder-1", item.FolderId);
        Assert.Equal("invoice-edf", item.ContextKey);
    }

    [Fact]
    public async Task Long_values_are_truncated()
    {
        var repo = new InMemoryLearningRepository();
        var service = new LocalLearningService(repo);

        await service.RecordAsync(
            LearningEventType.ManualSearch,
            value: new string('A', 1000));

        var events = await service.GetEventsAsync();

        Assert.Equal(200, events[0].Value!.Length);
    }

    [Fact]
    public async Task Clear_removes_all_learning()
    {
        var repo = new InMemoryLearningRepository();
        var service = new LocalLearningService(repo);

        await service.RecordAsync(
            LearningEventType.DestinationChosen);

        await service.ClearAsync();

        Assert.Empty(await service.GetEventsAsync());
    }

    [Fact]
    public async Task Events_are_returned_newest_first()
    {
        var repo = new InMemoryLearningRepository();

        await repo.AddAsync(
            new LearningEvent(
                "old",
                LearningEventType.DestinationChosen,
                DateTime.UtcNow.AddMinutes(-10),
                null,
                null,
                null));

        await repo.AddAsync(
            new LearningEvent(
                "new",
                LearningEventType.DestinationChosen,
                DateTime.UtcNow,
                null,
                null,
                null));

        var events = await repo.GetAllAsync();

        Assert.Equal("new", events[0].Id);
        Assert.Equal("old", events[1].Id);
    }
}
