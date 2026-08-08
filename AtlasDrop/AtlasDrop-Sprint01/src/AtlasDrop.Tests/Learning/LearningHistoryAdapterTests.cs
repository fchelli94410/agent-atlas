using Xunit;
using AtlasDrop.Core.Learning;
using AtlasDrop.Core.Suggestions;

namespace AtlasDrop.Tests.Learning;

public sealed class LearningHistoryAdapterTests
{
    [Theory]
    [InlineData(
        LearningEventType.DestinationChosen,
        UserHistorySignalType.Chosen)]
    [InlineData(
        LearningEventType.DestinationRejected,
        UserHistorySignalType.Rejected)]
    [InlineData(
        LearningEventType.FolderCreated,
        UserHistorySignalType.CreatedFolder)]
    [InlineData(
        LearningEventType.RenameCorrected,
        UserHistorySignalType.RenameCorrected)]
    [InlineData(
        LearningEventType.OperationUndone,
        UserHistorySignalType.Undo)]
    public void Learning_events_feed_scoring_history(
        LearningEventType input,
        UserHistorySignalType expected)
    {
        var events = new[]
        {
            new LearningEvent(
                "1",
                input,
                DateTime.UtcNow,
                "folder-1",
                "invoice-edf",
                null)
        };

        var result =
            LearningHistoryAdapter.ToUserHistorySignals(events);

        var signal = Assert.Single(result);
        Assert.Equal(expected, signal.Type);
        Assert.Equal("folder-1", signal.FolderId);
    }

    [Fact]
    public void Events_without_folder_are_ignored_for_folder_scoring()
    {
        var events = new[]
        {
            new LearningEvent(
                "1",
                LearningEventType.ManualSearch,
                DateTime.UtcNow,
                null,
                "invoice-edf",
                "edf")
        };

        Assert.Empty(
            LearningHistoryAdapter.ToUserHistorySignals(events));
    }
}
