using Microsoft.Data.Sqlite;

namespace AtlasDrop.Infrastructure.Database;

public sealed class AtlasDropDatabase : IAtlasDropDatabase
{
    public const int CurrentSchemaVersion = 2;
    public string DatabasePath { get; }

    public AtlasDropDatabase(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("Le chemin SQLite est obligatoire.", nameof(databasePath));

        DatabasePath = Path.GetFullPath(databasePath);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(DatabasePath)
                        ?? throw new InvalidOperationException("Dossier SQLite invalide.");

        Directory.CreateDirectory(directory);

        await using var connection = new SqliteConnection($"Data Source={DatabasePath};Mode=ReadWriteCreate;Cache=Shared");
        await connection.OpenAsync(cancellationToken);

        await ExecuteAsync(connection, """
            PRAGMA journal_mode=WAL;
            PRAGMA foreign_keys=ON;

            CREATE TABLE IF NOT EXISTS SchemaInfo (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                Version INTEGER NOT NULL,
                UpdatedUtc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Folders (
                StableId TEXT PRIMARY KEY,
                FullPath TEXT NOT NULL UNIQUE,
                Name TEXT NOT NULL,
                NormalizedName TEXT NOT NULL,
                ParentStableId TEXT NULL,
                Depth INTEGER NOT NULL,
                CreatedUtc TEXT NOT NULL,
                ModifiedUtc TEXT NOT NULL,
                FileCount INTEGER NOT NULL DEFAULT 0,
                FileTypesJson TEXT NOT NULL DEFAULT '[]',
                FrequentKeywordsJson TEXT NOT NULL DEFAULT '[]',
                FrequentPlacesJson TEXT NOT NULL DEFAULT '[]',
                FrequentCompaniesJson TEXT NOT NULL DEFAULT '[]',
                FrequentYearsJson TEXT NOT NULL DEFAULT '[]',
                IsExcluded INTEGER NOT NULL DEFAULT 0,
                QualityScore REAL NOT NULL DEFAULT 0,
                UsageCount INTEGER NOT NULL DEFAULT 0,
                LastUsedUtc TEXT NULL,
                LastScanUtc TEXT NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1,
                MissingSinceUtc TEXT NULL,
                FOREIGN KEY (ParentStableId) REFERENCES Folders(StableId) ON DELETE SET NULL
            );

            CREATE INDEX IF NOT EXISTS IX_Folders_NormalizedName ON Folders(NormalizedName);
            CREATE INDEX IF NOT EXISTS IX_Folders_ParentStableId ON Folders(ParentStableId);
            CREATE INDEX IF NOT EXISTS IX_Folders_Depth ON Folders(Depth);
            CREATE INDEX IF NOT EXISTS IX_Folders_IsExcluded ON Folders(IsExcluded);
            CREATE INDEX IF NOT EXISTS IX_Folders_IsActive ON Folders(IsActive);

            CREATE TABLE IF NOT EXISTS Operations (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SourcePath TEXT NOT NULL,
                OldName TEXT NULL,
                NewName TEXT NULL,
                OldDestination TEXT NULL,
                NewDestination TEXT NULL,
                CreatedUtc TEXT NOT NULL,
                Result TEXT NOT NULL,
                Sha256 TEXT NULL,
                IsUndone INTEGER NOT NULL DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS IX_Operations_CreatedUtc ON Operations(CreatedUtc DESC);

            CREATE TABLE IF NOT EXISTS LearningEvents (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                EventType TEXT NOT NULL,
                SourceFingerprint TEXT NULL,
                FolderStableId TEXT NULL,
                PayloadJson TEXT NOT NULL DEFAULT '{}',
                CreatedUtc TEXT NOT NULL,
                FOREIGN KEY (FolderStableId) REFERENCES Folders(StableId) ON DELETE SET NULL
            );

            CREATE INDEX IF NOT EXISTS IX_LearningEvents_FolderStableId ON LearningEvents(FolderStableId);
            CREATE INDEX IF NOT EXISTS IX_LearningEvents_CreatedUtc ON LearningEvents(CreatedUtc DESC);
            """, cancellationToken);

        if (!await ColumnExistsAsync(connection, "Folders", "IsActive", cancellationToken))
            await ExecuteAsync(connection, "ALTER TABLE Folders ADD COLUMN IsActive INTEGER NOT NULL DEFAULT 1;", cancellationToken);

        if (!await ColumnExistsAsync(connection, "Folders", "MissingSinceUtc", cancellationToken))
            await ExecuteAsync(connection, "ALTER TABLE Folders ADD COLUMN MissingSinceUtc TEXT NULL;", cancellationToken);

        await ExecuteAsync(connection, "CREATE INDEX IF NOT EXISTS IX_Folders_IsActive ON Folders(IsActive);", cancellationToken);

        await using var versionCommand = connection.CreateCommand();
        versionCommand.CommandText = """
            INSERT INTO SchemaInfo (Id, Version, UpdatedUtc)
            VALUES (1, $version, $utc)
            ON CONFLICT(Id) DO UPDATE SET
                Version = excluded.Version,
                UpdatedUtc = excluded.UpdatedUtc;
            """;
        versionCommand.Parameters.AddWithValue("$version", CurrentSchemaVersion);
        versionCommand.Parameters.AddWithValue("$utc", DateTime.UtcNow.ToString("O"));
        await versionCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<int> GetSchemaVersionAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(DatabasePath))
            return 0;

        await using var connection = new SqliteConnection($"Data Source={DatabasePath};Mode=ReadOnly");
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Version FROM SchemaInfo WHERE Id = 1 LIMIT 1;";

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null ? 0 : Convert.ToInt32(result);
    }

    private static async Task<bool> ColumnExistsAsync(
        SqliteConnection connection,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({table});";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
