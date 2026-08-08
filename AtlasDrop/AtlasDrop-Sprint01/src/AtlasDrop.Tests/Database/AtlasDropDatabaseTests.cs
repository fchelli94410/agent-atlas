using Microsoft.Data.Sqlite;
using Xunit;
using AtlasDrop.Infrastructure.Database;

namespace AtlasDrop.Tests.Database;

public sealed class AtlasDropDatabaseTests
{
    private static string CreateTempDbPath()
    {
        var root = Path.Combine(Path.GetTempPath(), "AtlasDrop.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return Path.Combine(root, "atlasdrop.db");
    }

    [Fact]
    public async Task Initialize_creates_database_file()
    {
        var dbPath = CreateTempDbPath();

        try
        {
            var database = new AtlasDropDatabase(dbPath);
            await database.InitializeAsync();

            Assert.True(File.Exists(dbPath));
        }
        finally
        {
            DeleteTree(Path.GetDirectoryName(dbPath)!);
        }
    }

    [Fact]
    public async Task Initialize_sets_schema_version_to_one()
    {
        var dbPath = CreateTempDbPath();

        try
        {
            var database = new AtlasDropDatabase(dbPath);
            await database.InitializeAsync();

            Assert.Equal(AtlasDropDatabase.CurrentSchemaVersion, await database.GetSchemaVersionAsync());
        }
        finally
        {
            DeleteTree(Path.GetDirectoryName(dbPath)!);
        }
    }

    [Theory]
    [InlineData("Folders")]
    [InlineData("Operations")]
    [InlineData("LearningEvents")]
    [InlineData("SchemaInfo")]
    public async Task Initialize_creates_required_tables(string tableName)
    {
        var dbPath = CreateTempDbPath();

        try
        {
            var database = new AtlasDropDatabase(dbPath);
            await database.InitializeAsync();

            await using var connection = new SqliteConnection($"Data Source={dbPath}");
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*)
                FROM sqlite_master
                WHERE type='table' AND name=$name;
                """;
            command.Parameters.AddWithValue("$name", tableName);

            var count = Convert.ToInt32(await command.ExecuteScalarAsync());
            Assert.Equal(1, count);
        }
        finally
        {
            DeleteTree(Path.GetDirectoryName(dbPath)!);
        }
    }

    [Fact]
    public async Task Initialize_is_idempotent()
    {
        var dbPath = CreateTempDbPath();

        try
        {
            var database = new AtlasDropDatabase(dbPath);

            await database.InitializeAsync();
            await database.InitializeAsync();

            Assert.Equal(AtlasDropDatabase.CurrentSchemaVersion, await database.GetSchemaVersionAsync());
        }
        finally
        {
            DeleteTree(Path.GetDirectoryName(dbPath)!);
        }
    }

    [Fact]
    public async Task Folder_full_path_is_unique()
    {
        var dbPath = CreateTempDbPath();

        try
        {
            var database = new AtlasDropDatabase(dbPath);
            await database.InitializeAsync();

            await using var connection = new SqliteConnection($"Data Source={dbPath}");
            await connection.OpenAsync();

            const string insert = """
                INSERT INTO Folders (
                    StableId, FullPath, Name, NormalizedName, ParentStableId, Depth,
                    CreatedUtc, ModifiedUtc, FileCount, FileTypesJson,
                    FrequentKeywordsJson, FrequentPlacesJson, FrequentCompaniesJson,
                    FrequentYearsJson, IsExcluded, QualityScore, UsageCount,
                    LastUsedUtc, LastScanUtc
                ) VALUES (
                    $id, $path, 'Test', 'test', NULL, 1,
                    $utc, $utc, 0, '[]', '[]', '[]', '[]', '[]',
                    0, 0, 0, NULL, $utc
                );
                """;

            var utc = DateTime.UtcNow.ToString("O");

            await using (var first = connection.CreateCommand())
            {
                first.CommandText = insert;
                first.Parameters.AddWithValue("$id", "A");
                first.Parameters.AddWithValue("$path", @"C:\Fake\Folder");
                first.Parameters.AddWithValue("$utc", utc);
                await first.ExecuteNonQueryAsync();
            }

            await using var second = connection.CreateCommand();
            second.CommandText = insert;
            second.Parameters.AddWithValue("$id", "B");
            second.Parameters.AddWithValue("$path", @"C:\Fake\Folder");
            second.Parameters.AddWithValue("$utc", utc);

            await Assert.ThrowsAsync<SqliteException>(() => second.ExecuteNonQueryAsync());
        }
        finally
        {
            DeleteTree(Path.GetDirectoryName(dbPath)!);
        }
    }

    private static void DeleteTree(string path)
    {
        if (!Directory.Exists(path))
            return;

        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);

            Directory.Delete(path, recursive: true);
        }
        catch
        {
            // Les fichiers SQLite peuvent rester brièvement verrouillés sur Windows.
            // Le dossier est sous %TEMP% et sera nettoyé par le système.
        }
    }
}
