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
    public void Create_folder_button_is_connected()
    {
        var code = File.ReadAllText(
            FindFile("MainWindow.xaml.cs"));

        Assert.Contains(
            "CreateFolderButton.Click += OnCreateFolderClicked",
            code,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Folder_creation_uses_safe_service()
    {
        var code = File.ReadAllText(
            FindFile("MainWindow.xaml.cs"));

        Assert.Contains(
            "SafeFolderCreationService",
            code,
            StringComparison.Ordinal);

        Assert.Contains(
            "FolderCreationRequest",
            code,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Created_folder_becomes_selected_manual_destination()
    {
        var code = File.ReadAllText(
            FindFile("MainWindow.xaml.cs"));

        Assert.Contains(
            "SelectedManualDestinationText.Text = result.FullPath",
            code,
            StringComparison.Ordinal);
    }
}
