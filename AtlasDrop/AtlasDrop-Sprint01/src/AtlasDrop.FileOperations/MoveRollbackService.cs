using AtlasDrop.Core.FileSystem;

namespace AtlasDrop.FileOperations;

public sealed class MoveRollbackService : IMoveRollbackService
{
    public RollbackResult TryRollback(
        string originalSourcePath,
        string destinationPath)
    {
        if (string.IsNullOrWhiteSpace(originalSourcePath))
            return new RollbackResult(false, "Chemin source original vide.");

        if (string.IsNullOrWhiteSpace(destinationPath))
            return new RollbackResult(false, "Chemin destination vide.");

        try
        {
            if (File.Exists(originalSourcePath))
            {
                return new RollbackResult(
                    false,
                    "Rollback impossible : la source originale existe déjà.");
            }

            if (!File.Exists(destinationPath))
            {
                return new RollbackResult(
                    false,
                    "Rollback impossible : le fichier destination est absent.");
            }

            var sourceDirectory = Path.GetDirectoryName(originalSourcePath);

            if (string.IsNullOrWhiteSpace(sourceDirectory))
            {
                return new RollbackResult(
                    false,
                    "Rollback impossible : dossier source introuvable.");
            }

            Directory.CreateDirectory(sourceDirectory);

            File.Move(
                destinationPath,
                originalSourcePath,
                overwrite: false);

            if (!File.Exists(originalSourcePath) ||
                File.Exists(destinationPath))
            {
                return new RollbackResult(
                    false,
                    "Rollback non vérifié.");
            }

            return new RollbackResult(
                true,
                "Rollback effectué et vérifié.");
        }
        catch (UnauthorizedAccessException)
        {
            return new RollbackResult(
                false,
                "Rollback impossible : accès refusé.");
        }
        catch (IOException ex)
        {
            return new RollbackResult(
                false,
                $"Rollback impossible : {ex.Message}");
        }
    }
}
