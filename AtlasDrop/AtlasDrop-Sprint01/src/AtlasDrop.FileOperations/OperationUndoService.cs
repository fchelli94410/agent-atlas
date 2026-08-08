using AtlasDrop.Core.FileSystem;
using AtlasDrop.Core.History;
using AtlasDrop.Infrastructure.FileSystem;

namespace AtlasDrop.FileOperations;

public sealed class OperationUndoService : IOperationUndoService
{
    private readonly IOperationHistoryRepository _history;
    private readonly IFileHashService _hashService;

    public OperationUndoService(
        IOperationHistoryRepository history,
        IFileHashService? hashService = null)
    {
        _history = history
            ?? throw new ArgumentNullException(nameof(history));

        _hashService = hashService
            ?? new FileHashService();
    }

    public async Task<UndoOperationResult> UndoAsync(
        OperationHistoryEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (!entry.Success)
        {
            return new UndoOperationResult(
                false,
                "Impossible d'annuler une opération en échec.",
                null);
        }

        if (entry.Undone)
        {
            return new UndoOperationResult(
                false,
                "Cette opération a déjà été annulée.",
                entry.SourcePath);
        }

        if (string.IsNullOrWhiteSpace(entry.NewDestination))
        {
            return new UndoOperationResult(
                false,
                "Destination actuelle inconnue.",
                null);
        }

        var currentPath = Path.Combine(
            entry.NewDestination,
            entry.NewFileName);

        if (!File.Exists(currentPath))
        {
            return new UndoOperationResult(
                false,
                "Le fichier déplacé n'existe plus à son emplacement actuel.",
                null);
        }

        if (File.Exists(entry.SourcePath))
        {
            return new UndoOperationResult(
                false,
                "Annulation refusée : un fichier existe déjà à l'emplacement d'origine.",
                null);
        }

        if (!string.IsNullOrWhiteSpace(entry.Sha256))
        {
            string currentHash;

            try
            {
                currentHash = _hashService.ComputeSha256(currentPath);
            }
            catch (Exception ex) when (
                ex is IOException or UnauthorizedAccessException)
            {
                return new UndoOperationResult(
                    false,
                    "Impossible de vérifier que le fichier est inchangé.",
                    null);
            }

            if (!string.Equals(
                currentHash,
                entry.Sha256,
                StringComparison.OrdinalIgnoreCase))
            {
                return new UndoOperationResult(
                    false,
                    "Annulation refusée : le fichier a changé depuis l'opération.",
                    null);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        var sourceDirectory = Path.GetDirectoryName(entry.SourcePath);

        if (string.IsNullOrWhiteSpace(sourceDirectory))
        {
            return new UndoOperationResult(
                false,
                "Dossier d'origine invalide.",
                null);
        }

        try
        {
            Directory.CreateDirectory(sourceDirectory);

            await Task.Run(
                () => File.Move(
                    currentPath,
                    entry.SourcePath,
                    overwrite: false),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            return new UndoOperationResult(
                false,
                "Accès refusé pendant l'annulation.",
                null);
        }
        catch (IOException ex)
        {
            return new UndoOperationResult(
                false,
                $"Annulation impossible : {ex.Message}",
                null);
        }

        if (!File.Exists(entry.SourcePath) ||
            File.Exists(currentPath))
        {
            return new UndoOperationResult(
                false,
                "Annulation non vérifiée.",
                null);
        }

        await _history.MarkUndoneAsync(
            entry.Id,
            cancellationToken);

        return new UndoOperationResult(
            true,
            "Annulation effectuée et vérifiée.",
            entry.SourcePath);
    }
}
