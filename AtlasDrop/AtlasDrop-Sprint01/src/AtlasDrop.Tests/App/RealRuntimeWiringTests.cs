using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class RealRuntimeWiringTests
{
    private static string FindFile(string project, string relativePath)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(
                current.FullName,
                "src",
                project,
                relativePath);

            if (File.Exists(candidate))
                return candidate;

            current = current.Parent;
        }

        throw new FileNotFoundException(relativePath);
    }

    [Fact]
    public void Demo_seed_data_is_removed()
    {
        var code = File.ReadAllText(
            FindFile("AtlasDrop.App", "MainWindow.xaml.cs"));

        Assert.DoesNotContain(
            "SeedPreviewSearchData",
            code,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            @"C:\OneDrive\Clients\EDF",
            code,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "Aperçu UI",
            code,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Real_file_is_analyzed()
    {
        var code = File.ReadAllText(
            FindFile("AtlasDrop.App", "MainWindow.xaml.cs"));

        Assert.Contains(
            "AnalyzeActiveFileAsync",
            code,
            StringComparison.Ordinal);

        Assert.Contains(
            "DocxTextExtractor",
            code,
            StringComparison.Ordinal);

        Assert.Contains(
            "DocumentClassificationService",
            code,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Real_onedrive_folders_are_loaded()
    {
        var code = File.ReadAllText(
            FindFile("AtlasDrop.App", "MainWindow.xaml.cs"));

        Assert.Contains(
            "EnumerateFoldersSafe",
            code,
            StringComparison.Ordinal);

        Assert.Contains(
            "_options.OneDriveRoot",
            code,
            StringComparison.Ordinal);

        Assert.Contains(
            "_options.MaxSuggestedDepth",
            code,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Classer_calls_safe_move_service()
    {
        var code = File.ReadAllText(
            FindFile("AtlasDrop.App", "MainWindow.xaml.cs"));

        Assert.Contains(
            "_moveService.MoveAsync",
            code,
            StringComparison.Ordinal);

        Assert.Contains(
            "SafeMoveRequest",
            code,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Xaml_has_no_edf_courbevoie_demo()
    {
        var xaml = File.ReadAllText(
            FindFile("AtlasDrop.App", "MainWindow.xaml"));

        Assert.DoesNotContain(
            "Facture EDF Courbevoie 2026.pdf",
            xaml,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            @"C:\OneDrive\Clients\EDF",
            xaml,
            StringComparison.Ordinal);
    }
}
