using AtlasDrop.Core.Integration;
using AtlasDrop.Infrastructure.Integration;

namespace AtlasDrop.Installer;

public sealed class InstallerEngine
{
    private readonly IWindowsContextMenuService _contextMenu;

    public InstallerEngine(
        IWindowsContextMenuService? contextMenu = null)
    {
        _contextMenu = contextMenu
            ?? new WindowsContextMenuService();
    }

    public InstallResult Install(string payloadDirectory)
    {
        if (string.IsNullOrWhiteSpace(payloadDirectory))
            return new InstallResult(false, "Payload vide.");

        string payload;

        try
        {
            payload = Path.GetFullPath(payloadDirectory);
        }
        catch
        {
            return new InstallResult(false, "Payload invalide.");
        }

        var payloadExe = Path.Combine(
            payload,
            "AtlasDrop.App.exe");

        if (!Directory.Exists(payload) ||
            !File.Exists(payloadExe))
        {
            return new InstallResult(
                false,
                "Payload Atlas Drop incomplet.");
        }

        var appDirectory = InstallPaths.GetAppDirectory();
        var installedExe = InstallPaths.GetInstalledExePath();

        try
        {
            Directory.CreateDirectory(
                InstallPaths.GetInstallRoot());

            if (Directory.Exists(appDirectory))
                Directory.Delete(appDirectory, recursive: true);

            CopyDirectory(payload, appDirectory);

            if (!File.Exists(installedExe))
            {
                return new InstallResult(
                    false,
                    "EXE installé introuvable.");
            }

            var menu = _contextMenu.Register(installedExe);

            if (!menu.Success)
            {
                return new InstallResult(
                    false,
                    "Application copiée mais menu contextuel non enregistré : "
                    + menu.Message);
            }

            return new InstallResult(
                true,
                "Atlas Drop installé.",
                installedExe);
        }
        catch (UnauthorizedAccessException)
        {
            return new InstallResult(
                false,
                "Installation refusée : accès interdit.");
        }
        catch (IOException ex)
        {
            return new InstallResult(
                false,
                $"Installation impossible : {ex.Message}");
        }
    }

    public InstallResult Uninstall()
    {
        var menu = _contextMenu.Unregister();

        try
        {
            var appDirectory = InstallPaths.GetAppDirectory();

            if (Directory.Exists(appDirectory))
                Directory.Delete(appDirectory, recursive: true);

            return menu.Success
                ? new InstallResult(
                    true,
                    "Atlas Drop désinstallé.")
                : new InstallResult(
                    false,
                    "Application supprimée mais menu contextuel non supprimé : "
                    + menu.Message);
        }
        catch (UnauthorizedAccessException)
        {
            return new InstallResult(
                false,
                "Désinstallation refusée : accès interdit.");
        }
        catch (IOException ex)
        {
            return new InstallResult(
                false,
                $"Désinstallation impossible : {ex.Message}");
        }
    }

    public static bool SelfTestPayload(string payloadDirectory)
    {
        if (string.IsNullOrWhiteSpace(payloadDirectory))
            return false;

        try
        {
            var payload = Path.GetFullPath(payloadDirectory);

            return Directory.Exists(payload)
                && File.Exists(
                    Path.Combine(payload, "AtlasDrop.App.exe"))
                && Directory
                    .EnumerateFiles(payload, "*.dll")
                    .Any();
        }
        catch
        {
            return false;
        }
    }

    private static void CopyDirectory(
        string source,
        string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.EnumerateFiles(source))
        {
            var target = Path.Combine(
                destination,
                Path.GetFileName(file));

            File.Copy(file, target, overwrite: true);
        }

        foreach (var directory in Directory.EnumerateDirectories(source))
        {
            var target = Path.Combine(
                destination,
                Path.GetFileName(directory));

            CopyDirectory(directory, target);
        }
    }
}
