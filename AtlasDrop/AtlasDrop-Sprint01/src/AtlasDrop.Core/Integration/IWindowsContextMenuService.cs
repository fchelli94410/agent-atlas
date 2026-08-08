namespace AtlasDrop.Core.Integration;

public interface IWindowsContextMenuService
{
    ContextMenuRegistrationResult Register(string executablePath);

    ContextMenuRegistrationResult Unregister();
}
