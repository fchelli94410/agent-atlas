namespace AtlasDrop.Installer;

public sealed record InstallResult(
    bool Success,
    string Message,
    string? InstalledExePath = null);
