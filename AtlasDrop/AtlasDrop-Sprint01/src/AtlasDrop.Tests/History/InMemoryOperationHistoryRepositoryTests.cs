using Xunit;
using AtlasDrop.Core.History;
using AtlasDrop.Infrastructure.History;

namespace AtlasDrop.Tests.History;

public sealed class InMemoryOperationHistoryRepositoryTests
{
    [Fact]
    public async Task Recent_history_returns_latest_first()
    {
        var repo = new InMemoryOperationHistoryRepository();

        await repo.AddAsync(Entry(
            "1",
            DateTime.UtcNow.AddMinutes(-10)));

        await repo.AddAsync(Entry(
            "2",
            DateTime.UtcNow));

        var result = await repo.GetRecentAsync();

        Assert.Equal("2", result[0].Id);
        Assert.Equal("1", result[1].Id);
    }

    [Fact]
    public async Task Default_history_limit_is_ten()
    {
        var repo = new InMemoryOperationHistoryRepository();

        for (var i = 0; i < 20; i++)
        {
            await repo.AddAsync(
                Entry(
                    i.ToString(),
                    DateTime.UtcNow.AddMinutes(-i)));
        }

        var result = await repo.GetRecentAsync();

        Assert.Equal(10, result.Count);
    }

    [Fact]
    public async Task Mark_undone_updates_entry()
    {
        var repo = new InMemoryOperationHistoryRepository();
        await repo.AddAsync(Entry("1", DateTime.UtcNow));

        await repo.MarkUndoneAsync("1");

        var result = await repo.GetRecentAsync();

        Assert.True(result[0].Undone);
    }

    private static OperationHistoryEntry Entry(
        string id,
        DateTime timestamp) =>
        new(
            id,
            @"C:\Source\a.pdf",
            "a.pdf",
            "b.pdf",
            @"C:\Source",
            @"C:\Dest",
            timestamp,
            true,
            "ok",
            "ABC",
            false);
}
