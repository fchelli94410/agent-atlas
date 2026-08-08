using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class MainWindowFolderCreationTests
{
    private static string FindFile(string relativePath)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(
                current.FullName,
                "src",
                "AtlasDrop.App",
                relativePath);

            if (File.Exists(candidate))
                return candidate;

            current = current.Parent;
        }

        throw new FileNotFoundException(relativePath);
    }

    [Fact]
    public void Obsolete_internal_folder_creation_controls_are_removed()
    {
        var xaml = File.ReadAllText(FindFile("MainWindow.xaml"));
        var code = File.ReadAllText(FindFile("MainWindow.xaml.cs"));

        Assert.DoesNotContain("CreateFolderButton", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("NewFolderNameTextBox", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("SafeFolderCreationService", code, StringComparison.Ordinal);
        Assert.DoesNotContain("FolderCreationRequest", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Wrong_folder_starts_explorer_at_onedrive_root()
    {
        var code = File.ReadAllText(FindFile("MainWindow.xaml.cs"));

        Assert.Contains(
            "await EnterExplorerRefinementModeAsync(_oneDriveRoot)",
            code,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Current_tracked_explorer_path_becomes_the_destination()
    {
        var code = File.ReadAllText(FindFile("MainWindow.xaml.cs"));

        Assert.Contains("TryGetExplorerPathByHwnd", code, StringComparison.Ordinal);
        Assert.Contains("ExecuteMoveOnceAsync(destination)", code, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectedManualDestinationText", code, StringComparison.Ordinal);
    }
}
