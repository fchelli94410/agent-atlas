using Xunit;

namespace AtlasDrop.Tests.Performance;

public sealed class Sprint58SafetyRegressionTests
{
    private static string FindFile(
        string project,
        string relativePath)
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
    public void Max_suggested_depth_is_five_in_v107()
    {
        var file = FindFile(
            "AtlasDrop.Core",
            Path.Combine("Configuration", "AtlasDropOptions.cs"));

        var code = File.ReadAllText(file);

        Assert.Contains(
            "MaxSuggestedDepth",
            code,
            StringComparison.Ordinal);

        Assert.Contains(
            "= 5",
            code,
            StringComparison.Ordinal);
    }

    [Fact]
    public void File_move_still_disallows_overwrite()
    {
        var file = FindFile(
            "AtlasDrop.FileOperations",
            "SafeFileMoveService.cs");

        var code = File.ReadAllText(file);

        Assert.Contains(
            "overwrite: false",
            code,
            StringComparison.Ordinal);
    }
}
