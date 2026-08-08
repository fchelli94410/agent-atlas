using AtlasDrop.Core.FileSystem;
using AtlasDrop.Infrastructure.FileSystem;

namespace AtlasDrop.FileOperations;

public sealed class SafeFileMoveService : ISafeFileMoveService
{
    private readonly IMovePreflightService _preflight;
    private readonly IFileHashService _hashService;
    private readonly IMoveVerificationService _verification;
    private readonly IMoveRollbackService _rollback;

    public SafeFileMoveService(
        IMovePreflightService? preflight = null,
        IFileHashService? hashService = null,
        IMoveVerificationService? verification = null,
        IMoveRollbackService? rollback = null)
    {
        _preflight = preflight
            ?? new MovePreflightService();

        _hashService = hashService
            ?? new FileHashService();

        _verification = verification
            ?? new MoveVerificationService(_hashService);

        _rollback = rollback
            ?? new MoveRollbackService();
    }

    public async Task<SafeMoveResult> MoveAsync(
        SafeMoveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var preflight = _preflight.Validate(
            new MovePreflightRequest(
                request.OneDriveRoot,
                request.SourceFilePath,
                request.DestinationDirectory,
                request.ProposedFileName));

        if (!preflight.CanProceed)
        {
            return new SafeMoveResult(
                false,
                preflight.SourcePath,
                preflight.DestinationPath,
                false,
                preflight.Duplicate.MatchKind ==
                    DuplicateMatchKind.ExactDuplicate,
                string.Join(" ", preflight.Errors));
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (preflight.Duplicate.MatchKind ==
            DuplicateMatchKind.ExactDuplicate)
        {
            return new SafeMoveResult(
                false,
                preflight.SourcePath,
                preflight.Duplicate.DestinationPath,
                false,
                true,
                "Doublon exact détecté : aucun déplacement effectué.");
        }

        var targetPath = preflight.Duplicate.DestinationExists
            ? preflight.Duplicate.SuggestedAvailablePath
            : preflight.DestinationPath;

        var renamedForConflict =
            !string.Equals(
                targetPath,
                preflight.DestinationPath,
                StringComparison.OrdinalIgnoreCase);

        long expectedSize;
        string? expectedHash = null;

        try
        {
            var sourceInfo = new FileInfo(preflight.SourcePath);
            expectedSize = sourceInfo.Length;

            if (expectedSize <= 64L * 1024L * 1024L)
            {
                expectedHash = _hashService.ComputeSha256(
                    preflight.SourcePath);
            }
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            return new SafeMoveResult(
                false,
                preflight.SourcePath,
                targetPath,
                renamedForConflict,
                false,
                "Impossible de préparer la vérification avant déplacement.");
        }

        try
        {
            await Task.Run(
                () => File.Move(
                    preflight.SourcePath,
                    targetPath,
                    overwrite: false),
                cancellationToken);

            var verification = _verification.Verify(
                preflight.SourcePath,
                targetPath,
                expectedSize,
                expectedHash);

            if (!verification.Success)
            {
                var rollback = _rollback.TryRollback(
                    preflight.SourcePath,
                    targetPath);

                var message = rollback.Success
                    ? verification.Message + " Rollback effectué."
                    : verification.Message + " " + rollback.Message;

                return new SafeMoveResult(
                    false,
                    preflight.SourcePath,
                    targetPath,
                    renamedForConflict,
                    false,
                    message);
            }

            return new SafeMoveResult(
                true,
                preflight.SourcePath,
                targetPath,
                renamedForConflict,
                false,
                renamedForConflict
                    ? "Fichier déplacé et vérifié avec renommage anti-écrasement."
                    : "Fichier déplacé et vérifié.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            return new SafeMoveResult(
                false,
                preflight.SourcePath,
                targetPath,
                renamedForConflict,
                false,
                "Accès refusé pendant le déplacement.");
        }
        catch (IOException ex)
        {
            return new SafeMoveResult(
                false,
                preflight.SourcePath,
                targetPath,
                renamedForConflict,
                false,
                $"Échec du déplacement : {ex.Message}");
        }
    }
}
