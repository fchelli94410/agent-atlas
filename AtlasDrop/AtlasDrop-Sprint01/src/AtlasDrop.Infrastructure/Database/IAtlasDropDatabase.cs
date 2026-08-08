namespace AtlasDrop.Infrastructure.Database;

public interface IAtlasDropDatabase
{
    string DatabasePath { get; }
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<int> GetSchemaVersionAsync(CancellationToken cancellationToken = default);
}
