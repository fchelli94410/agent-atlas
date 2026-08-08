using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class MainWindowWorkflowTests
{
    private static string ReadCode()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "AtlasDrop.App", "MainWindow.xaml.cs");
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            current = current.Parent;
        }
        throw new FileNotFoundException("MainWindow.xaml.cs introuvable.");
    }

    [Theory]
    [InlineData("MaxDepth = 4")]
    [InlineData("CountItems(_activePath)")]
    [InlineData("RecordLearning")]
    [InlineData("AvailableTarget")]
    [InlineData("OpenExplorer")]
    [InlineData("File.SetLastWriteTimeUtc")]
    public void Required_v107_safety_features_are_present(string token) => Assert.Contains(token, ReadCode(), StringComparison.Ordinal);

    [Fact]
    public void Folder_analysis_does_not_recurse()
    {
        var code = ReadCode();
        Assert.Contains("Directory.EnumerateFileSystemEntries(path)", code, StringComparison.Ordinal);
        Assert.DoesNotContain("EnumerateFileSystemEntries(path,", code, StringComparison.Ordinal);
    }
}
