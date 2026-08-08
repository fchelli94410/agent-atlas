using Xunit;
using AtlasDrop.Core.History;
using AtlasDrop.FileOperations;
using AtlasDrop.Infrastructure.FileSystem;
using AtlasDrop.Infrastructure.History;

namespace AtlasDrop.Tests.History;

public sealed class OperationUndoServiceTests : IDisposable
{
    private readonly string _root;
    private readonly string _sourceDir;
    private readonly string _destDir;

    public OperationUndoServiceTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "AtlasDropUndoTests",
            Guid.NewGuid().ToString("N"));

        _sourceDir = Path.Combine(_root, "Source");
        _destDir = Path.Combine(_root, "Dest");

        Directory.CreateDirectory(_sourceDir);
        Directory.CreateDirectory(_destDir);
    }

    [Fact]
    public async Task Successful_operation_can_be_undone()
    {
        var currentPath = Path.Combine(_destDir, "facture.pdf");
        File.WriteAllText(currentPath, "atlas-drop");

        var hash = new FileHashService().ComputeSha256(currentPath);

        var entry = Entry(
            currentPath,
            hash);

        var repo = new InMemoryOperationHistoryRepository();
        await repo.AddAsync(entry);

        var result = await new OperationUndoService(repo).UndoAsync(entry);

        Assert.True(result.Success);
        Assert.True(File.Exists(entry.SourcePath));
        Assert.False(File.Exists(currentPath));

        var history = await repo.GetRecentAsync();
        Assert.True(history[0].Undone);
    }

    [Fact]
    public async Task Changed_file_is_not_undone()
    {
        var currentPath = Path.Combine(_destDir, "facture.pdf");
        File.WriteAllText(currentPath, "original");

        var originalHash = new FileHashService().ComputeSha256(currentPath);

        File.WriteAllText(currentPath, "modified");

        var entry = Entry(
            currentPath,
            originalHash);

        var repo = new InMemoryOperationHistoryRepository();
        await repo.AddAsync(entry);

        var result = await new OperationUndoService(repo).UndoAsync(entry);

        Assert.False(result.Success);
        Assert.True(File.Exists(currentPath));
        Assert.False(File.Exists(entry.SourcePath));
    }

    [Fact]
    public async Task Existing_original_path_prevents_overwrite()
    {
        var currentPath = Path.Combine(_destDir, "facture.pdf");
        File.WriteAllText(currentPath, "moved");

        var entry = Entry(
            currentPath,
            null);

        File.WriteAllText(entry.SourcePath, "existing");

        var repo = new InMemoryOperationHistoryRepository();

        var result = await new OperationUndoService(repo).UndoAsync(entry);

        Assert.False(result.Success);
        Assert.Equal("existing", File.ReadAllText(entry.SourcePath));
        Assert.Equal("moved", File.ReadAllText(currentPath));
    }

    [Fact]
    public async Task Failed_history_entry_cannot_be_undone()
    {
        var currentPath = Path.Combine(_destDir, "facture.pdf");
        File.WriteAllText(currentPath, "moved");

        var entry = Entry(
            currentPath,
            null) with
        {
            Success = false
        };

        var result = await new OperationUndoService(
            new InMemoryOperationHistoryRepository())
            .UndoAsync(entry);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Already_undone_entry_is_rejected()
    {
        var currentPath = Path.Combine(_destDir, "facture.pdf");
        File.WriteAllText(currentPath, "moved");

        var entry = Entry(
            currentPath,
            null) with
        {
            Undone = true
        };

        var result = await new OperationUndoService(
            new InMemoryOperationHistoryRepository())
            .UndoAsync(entry);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Missing_current_file_is_rejected()
    {
        var entry = Entry(
            Path.Combine(_destDir, "missing.pdf"),
            null);

        var result = await new OperationUndoService(
            new InMemoryOperationHistoryRepository())
            .UndoAsync(entry);

        Assert.False(result.Success);
    }

    private OperationHistoryEntry Entry(
        string currentPath,
        string? hash)
    {
        var sourcePath = Path.Combine(
            _sourceDir,
            "source.pdf");

        return new OperationHistoryEntry(
            "op1",
            sourcePath,
            "source.pdf",
            Path.GetFileName(currentPath),
            _sourceDir,
            _destDir,
            DateTime.UtcNow,
            true,
            "ok",
            hash,
            false);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
        }
    }
}
