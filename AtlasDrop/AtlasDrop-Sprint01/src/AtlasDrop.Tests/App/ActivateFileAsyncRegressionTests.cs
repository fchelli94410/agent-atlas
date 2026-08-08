using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class ActivateFileAsyncRegressionTests
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
    public void Activate_file_is_async_and_runs_real_analysis()
    {
        var code = File.ReadAllText(
            FindFile("MainWindow.xaml.cs"));

        Assert.Contains(
            "public async void ActivateFile(string filePath)",
            code,
            StringComparison.Ordinal);

        Assert.Contains(
            "await LoadAutomaticFolderCandidatesAsync()",
            code,
            StringComparison.Ordinal);

        Assert.Contains(
            "await AnalyzeActiveFileAsync()",
            code,
            StringComparison.Ordinal);
    }
}
