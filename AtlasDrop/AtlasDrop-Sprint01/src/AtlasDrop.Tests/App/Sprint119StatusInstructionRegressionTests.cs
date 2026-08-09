using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class Sprint119StatusInstructionRegressionTests
{
    private static string FindFile(string relativePath)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "AtlasDrop.App", relativePath);
            if (File.Exists(candidate)) return candidate;
            current = current.Parent;
        }

        throw new FileNotFoundException(relativePath);
    }

    [Fact]
    public void Idle_validation_instruction_is_force_collapsed_even_when_runtime_sets_status_visible()
    {
        var code = File.ReadAllText(FindFile("MainWindow.StatusInstructionFilter.cs"));

        Assert.Contains("Valide explicitement avant tout déplacement.", code, StringComparison.Ordinal);
        Assert.Contains("StatusText.Visibility = Visibility.Collapsed", code, StringComparison.Ordinal);
        Assert.Contains("DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Other_status_messages_remain_visible()
    {
        var code = File.ReadAllText(FindFile("MainWindow.StatusInstructionFilter.cs"));

        Assert.Contains("StatusText.Visibility = Visibility.Visible", code, StringComparison.Ordinal);
        Assert.Contains("string.Equals", code, StringComparison.Ordinal);
    }
}
