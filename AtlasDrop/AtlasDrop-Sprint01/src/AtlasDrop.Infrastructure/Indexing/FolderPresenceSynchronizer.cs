using AtlasDrop.Core.Indexing;
using Microsoft.Data.Sqlite;

namespace AtlasDrop.Infrastructure.Indexing;

public sealed class FolderPresenceSynchronizer : IFolderPresenceSynchronizer
{
    private readonly string _databasePath;

    public FolderPresenceSynchronizer(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("Le chemin SQLite est obligatoire.", nameof(databasePath));

        _databasePath = Path.GetFullPath(databasePath);
    }

    public async Task<FolderPresenceSyncResult> SynchronizeAsync(
        IReadOnlyCollection<string> existingFolderPaths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(existingFolderPaths);

        if (!File.Exists(_databasePath))
            throw new FileNotFoundException("La base Atlas Drop est introuvable.", _databasePath);

        var normalized = existingFolderPaths
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var now = DateTime.UtcNow;

        await using var connection = new SqliteConnection($"Data Source={_databasePath};Mode=ReadWrite");
        await connection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();

        await using (var markMissing = connection.CreateCommand())
        {
            markMissing.Transaction = transaction;
            markMissing.CommandText = """
                UPDATE Folders
                SET IsActive = 0,
                    MissingSinceUtc = COALESCE(MissingSinceUtc, $utc)
                WHERE IsActive <> 0;
                """;
            markMissing.Parameters.AddWithValue("$utc", now.ToString("O"));
            await markMissing.ExecuteNonQueryAsync(cancellationToken);
        }

        var markedActive = 0;
        foreach (var path in normalized)
        {
            await using var markActive = connection.CreateCommand();
            markActive.Transaction = transaction;
            markActive.CommandText = """
                UPDATE Folders
                SET IsActive = 1,
                    MissingSinceUtc = NULL,
                    LastScanUtc = $utc
                WHERE FullPath = $path COLLATE NOCASE;
                """;
            markActive.Parameters.AddWithValue("$utc", now.ToString("O"));
            markActive.Parameters.AddWithValue("$path", path);
            markedActive += await markActive.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var countMissing = connection.CreateCommand();
        countMissing.Transaction = transaction;
        countMissing.CommandText = "SELECT COUNT(*) FROM Folders WHERE IsActive = 0;";
        var markedMissing = Convert.ToInt32(await countMissing.ExecuteScalarAsync(cancellationToken));

        transaction.Commit();

        return new FolderPresenceSyncResult(markedActive, markedMissing, now);
    }

    private static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
