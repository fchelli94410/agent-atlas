using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class Sprint116LearningManagementTests
{
    private static string FindFile(string name)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "AtlasDrop.App", name);
            if (File.Exists(candidate)) return candidate;
            current = current.Parent;
        }

        throw new FileNotFoundException(name + " introuvable.");
    }

    [Fact]
    public void Learning_management_uses_the_new_three_button_window()
    {
        var code = File.ReadAllText(FindFile("MainWindow.Finishing.cs"));

        Assert.Contains("ManageLearningButton.Click -= OnManageLearningClicked", code, StringComparison.Ordinal);
        Assert.Contains("ManageLearningButton.Click += OnFinishingManageLearningClicked", code, StringComparison.Ordinal);
        Assert.Contains("Content = \"TOUT EFFACER\"", code, StringComparison.Ordinal);
        Assert.Contains("Content = \"SUPPRIMER LA SÉLECTION\"", code, StringComparison.Ordinal);
        Assert.Contains("Content = \"FERMER\"", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Clear_all_requires_confirmation_and_never_touches_onedrive_files()
    {
        var code = File.ReadAllText(FindFile("MainWindow.Finishing.cs"));
        var start = code.IndexOf("clearAll.Click +=", StringComparison.Ordinal);
        var end = code.IndexOf("deleteSelected.Click +=", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var block = code[start..end];

        Assert.Contains("MessageBoxButton.YesNo", block, StringComparison.Ordinal);
        Assert.Contains("MessageBoxResult.Yes", block, StringComparison.Ordinal);
        Assert.Contains("_learningService.ClearAsync()", block, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Move", block, StringComparison.Ordinal);
        Assert.DoesNotContain("Directory.Move", block, StringComparison.Ordinal);
        Assert.DoesNotContain("File.Delete(move", block, StringComparison.Ordinal);
    }

    [Fact]
    public void Learning_action_buttons_share_three_equal_columns()
    {
        var code = File.ReadAllText(FindFile("MainWindow.Finishing.cs"));
        Assert.True(code.Split("new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }", StringSplitOptions.None).Length - 1 >= 3);
        Assert.Contains("Grid.SetColumn(clearAll, 0)", code, StringComparison.Ordinal);
        Assert.Contains("Grid.SetColumn(deleteSelected, 1)", code, StringComparison.Ordinal);
        Assert.Contains("Grid.SetColumn(close, 2)", code, StringComparison.Ordinal);
    }
}
