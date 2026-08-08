using Microsoft.Win32;
using AtlasDrop.Core.Integration;

namespace AtlasDrop.Infrastructure.Integration;

public sealed class WindowsContextMenuService
    : IWindowsContextMenuService
{
    private const string BaseKeyPath =
        @"Software\Classes\*\shell\AtlasDrop";

    private const string CommandKeyPath =
        BaseKeyPath + @"\command";

    public ContextMenuRegistrationResult Register(
        string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return new ContextMenuRegistrationResult(
                false,
                "Chemin exécutable vide.");
        }

        string fullPath;

        try
        {
            fullPath = Path.GetFullPath(executablePath);
        }
        catch
        {
            return new ContextMenuRegistrationResult(
                false,
                "Chemin exécutable invalide.");
        }

        if (!File.Exists(fullPath))
        {
            return new ContextMenuRegistrationResult(
                false,
                "Exécutable Atlas Drop introuvable.");
        }

        try
        {
            using var baseKey = Registry.CurrentUser.CreateSubKey(
                BaseKeyPath,
                writable: true);

            if (baseKey is null)
            {
                return new ContextMenuRegistrationResult(
                    false,
                    "Impossible de créer la clé de menu contextuel.");
            }

            baseKey.SetValue(
                null,
                "Ranger avec Atlas Drop",
                RegistryValueKind.String);

            baseKey.SetValue(
                "Icon",
                Quote(fullPath),
                RegistryValueKind.String);

            using var commandKey = Registry.CurrentUser.CreateSubKey(
                CommandKeyPath,
                writable: true);

            if (commandKey is null)
            {
                return new ContextMenuRegistrationResult(
                    false,
                    "Impossible de créer la commande du menu contextuel.");
            }

            commandKey.SetValue(
                null,
                $"{Quote(fullPath)} \"%1\"",
                RegistryValueKind.String);

            return new ContextMenuRegistrationResult(
                true,
                "Menu contextuel Atlas Drop enregistré pour l'utilisateur courant.");
        }
        catch (UnauthorizedAccessException)
        {
            return new ContextMenuRegistrationResult(
                false,
                "Accès refusé au registre utilisateur.");
        }
        catch (System.Security.SecurityException)
        {
            return new ContextMenuRegistrationResult(
                false,
                "Accès au registre refusé.");
        }
    }

    public ContextMenuRegistrationResult Unregister()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(
                BaseKeyPath,
                throwOnMissingSubKey: false);

            return new ContextMenuRegistrationResult(
                true,
                "Menu contextuel Atlas Drop supprimé.");
        }
        catch (UnauthorizedAccessException)
        {
            return new ContextMenuRegistrationResult(
                false,
                "Accès refusé au registre utilisateur.");
        }
        catch (System.Security.SecurityException)
        {
            return new ContextMenuRegistrationResult(
                false,
                "Accès au registre refusé.");
        }
    }

    private static string Quote(string value) =>
        $"\"{value}\"";
}
