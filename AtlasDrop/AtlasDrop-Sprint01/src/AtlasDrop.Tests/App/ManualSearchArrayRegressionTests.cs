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
    public void Manual_search_array_uses_length_not_count_method_group()
    {
        var code = File.ReadAllText(FindFile());

        Assert.Contains(
            "if (results.Length == 0)",
            code,
            StringComparison.Ordinal);

        Assert.Contains(
            "{results.Length} dossier(s)",
            code,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "if (results.Count == 0)",
            code,
            StringComparison.Ordinal);
    }
}
