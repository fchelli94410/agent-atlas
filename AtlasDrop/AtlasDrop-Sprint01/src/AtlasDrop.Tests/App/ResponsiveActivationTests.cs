using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class ResponsiveActivationTests
{
    private static string FindFile()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(
                current.FullName,
                "src",
                "AtlasDrop.App",
                "MainWindow.xaml.cs");

            if (File.Exists(candidate))
                return candidate;

            current = current.Parent;
        }

        throw new FileNotFoundException("MainWindow.xaml.cs");
    }

    [Fact]
    public void File_analysis_runs_before_folder_enumeration()
    {
        var code = File.ReadAllText(FindFile());

        var analyze = code.IndexOf(
            "await AnalyzeActiveFileAsync()",
            StringComparison.Ordinal);

        var shallow = code.IndexOf(
            "await LoadAutomaticFolderCandidatesAsync()",
            StringComparison.Ordinal);

        Assert.True(analyze >= 0);
        Assert.True(shallow > analyze);
    }

    [Fact]
    public void Automatic_folder_scan_is_depth_limited()
    {
        var code = File.ReadAllText(FindFile());

        Assert.Contains(
            "EnumerateFoldersToDepth",
            code,
            StringComparison.Ordinal);

        Assert.Contains(
            "_options.MaxSuggestedDepth",
            code,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Manual_refinement_uses_tracked_explorer_without_deep_scan()
    {
        var code = File.ReadAllText(FindFile());

        Assert.DoesNotContain(
            "LoadDeepFoldersForManualSearchAsync",
            code,
            StringComparison.Ordinal);

        Assert.Contains(
            "OpenExplorerAndTrackAsync",
            code,
            StringComparison.Ordinal);

        Assert.Contains(
            "private const int MaxDepth = 4;",
            code,
            StringComparison.Ordinal);
    }
}
