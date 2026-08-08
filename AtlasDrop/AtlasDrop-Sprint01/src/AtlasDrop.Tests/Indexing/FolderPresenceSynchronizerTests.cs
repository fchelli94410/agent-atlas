using AtlasDrop.Infrastructure.Database;
using AtlasDrop.Infrastructure.Indexing;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AtlasDrop.Tests.Indexing;

public sealed class FolderPresenceSynchronizerTests
{
    [Fact]
    public async Task Missing_folder_is_marked_inactive_not_deleted()
    {
        var dbPath = CreateTempDbPath();
        try
        {
            await SeedFolderAsync(dbPath, "A", @"C:\Root\A");

            var synchronizer = new FolderPresenceSynchronizer(dbPath);
            var result = await synchronizer.SynchronizeAsync(Array.Empty<string>());

            Assert.Equal(1, result.MarkedMissing);
            var row = await ReadStateAsync(dbPath, "A");
            Assert.False(row.IsActive);
            Assert.NotNull(row.MissingSinceUtc);
        }
        finally { DeleteTree(Path.GetDirectoryName(dbPath)!); }
    }

    [Fact]
    public async Task Existing_folder_stays_active()
    {
        var dbPath = CreateTempDbPath();
        try
        {
            await SeedFolderAsync(dbPath, "A", @"C:\Root\A");

            var synchronizer = new FolderPresenceSynchronizer(dbPath);
            var result = await synchronizer.SynchronizeAsync(new[] { @"C:\Root\A" });

            Assert.Equal(1, result.MarkedActive);
            Assert.Equal(0, result.MarkedMissing);
            var row = await ReadStateAsync(dbPath, "A");
            Assert.True(row.IsActive);
            Assert.Null(row.MissingSinceUtc);
        }
        finally { DeleteTree(Path.GetDirectoryName(dbPath)!); }
    }

    [Fact]
    public async Task Reappearing_folder_is_reactivated()
    {
        var dbPath = CreateTempDbPath();
        try
        {
            await SeedFolderAsync(dbPath, "A", @"C:\Root\A");
            var synchronizer = new FolderPresenceSynchronizer(dbPath);

            await synchronizer.SynchronizeAsync(Array.Empty<string>());
            await synchronizer.SynchronizeAsync(new[] { @"c:\root\a" });

            var row = await ReadStateAsync(dbPath, "A");
            Assert.True(row.IsActive);
            Assert.Null(row.MissingSinceUtc);
        }
        finally { DeleteTree(Path.GetDirectoryName(dbPath)!); }
    }

    [Fact]
    public async Task Stable_id_and_history_fields_are_preserved()
    {
        var dbPath = CreateTempDbPath();
        try
        {
            await SeedFolderAsync(dbPath, "stable-history", @"C:\Root\A", usageCount: 42);
            var synchronizer = new FolderPresenceSynchronizer(dbPath);

            await synchronizer.SynchronizeAsync(Array.Empty<string>());

            await using var connection = new SqliteConnection($"Data Source={dbPath}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT StableId, UsageCount FROM Folders WHERE StableId='stable-history';";
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal("stable-history", reader.GetString(0));
            Assert.Equal(42, reader.GetInt32(1));
        }
        finally { DeleteTree(Path.GetDirectoryName(dbPath)!); }
    }

    [Fact]
    public async Task Schema_is_upgraded_to_version_two()
    {
        var dbPath = CreateTempDbPath();
        try
        {
            var database = new AtlasDropDatabase(dbPath);
            await database.InitializeAsync();

            Assert.Equal(2, await database.GetSchemaVersionAsync());
        }
        finally { DeleteTree(Path.GetDirectoryName(dbPath)!); }
    }

    private static async Task SeedFolderAsync(string dbPath, string stableId, string path, int usageCount = 0)
    {
        var database = new AtlasDropDatabase(dbPath);
        await database.InitializeAsync();

        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Folders (
                StableId, FullPath, Name, NormalizedName, ParentStableId, Depth,
                CreatedUtc, ModifiedUtc, FileCount, FileTypesJson,
                FrequentKeywordsJson, FrequentPlacesJson, FrequentCompaniesJson,
                FrequentYearsJson, IsExcluded, QualityScore, UsageCount,
                LastUsedUtc, LastScanUtc, IsActive, MissingSinceUtc
            ) VALUES (
                $id, $path, 'A', 'a', NULL, 1,
                $utc, $utc, 0, '[]', '[]', '[]', '[]', '[]',
                0, 0, $usage, NULL, $utc, 1, NULL
            );
            """;
        command.Parameters.AddWithValue("$id", stableId);
        command.Parameters.AddWithValue("$path", path);
        command.Parameters.AddWithValue("$usage", usageCount);
        command.Parameters.AddWithValue("$utc", DateTime.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<(bool IsActive, string? MissingSinceUtc)> ReadStateAsync(string dbPath, string stableId)
    {
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT IsActive, MissingSinceUtc FROM Folders WHERE StableId=$id;";
        command.Parameters.AddWithValue("$id", stableId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetInt32(0) == 1, reader.IsDBNull(1) ? null : reader.GetString(1));
    }

    private static string CreateTempDbPath()
    {
        var root = Path.Combine(Path.GetTempPath(), "AtlasDrop.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return Path.Combine(root, "atlasdrop.db");
    }

    private static void DeleteTree(string path)
    {
        if (!Directory.Exists(path)) return;
        try { Directory.Delete(path, true); } catch { }
    }
}
