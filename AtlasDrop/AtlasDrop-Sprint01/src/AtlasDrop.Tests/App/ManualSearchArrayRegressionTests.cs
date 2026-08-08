using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class ManualSearchArrayRegressionTests
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
    public void Obsolete_manual_search_array_is_removed()
    {
        var code = File.ReadAllText(FindFile());

        Assert.DoesNotContain("RunManualSearch", code, StringComparison.Ordinal);
        Assert.DoesNotContain("SearchResultsList", code, StringComparison.Ordinal);
        Assert.Contains("TryGetExplorerPathByHwnd", code, StringComparison.Ordinal);
    }
}
