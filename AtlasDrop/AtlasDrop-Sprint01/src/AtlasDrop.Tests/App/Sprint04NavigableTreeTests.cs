using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class Sprint04NavigableTreeTests
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
    public void Proposed_branch_is_opened_but_all_root_branches_remain_available()
    {
        var code = File.ReadAllText(FindFile("MainWindow.Finishing.cs"));
        var xaml = File.ReadAllText(FindFile("MainWindow.xaml"));

        Assert.Contains("LayoutUpdated=\"OnFolderTreeLayoutUpdated\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AllowedRootFolderNames.All(visibleRoots.Contains)", code, StringComparison.Ordinal);
        Assert.Contains("foreach (var folder in _folders", code, StringComparison.Ordinal);
        Assert.Contains("IsSameOrChild(_proposedFolder, folder.Path)", code, StringComparison.Ordinal);
        Assert.Contains("rootItem.IsExpanded = true", code, StringComparison.Ordinal);
        Assert.Contains("proposedNode.BringIntoView()", code, StringComparison.Ordinal);
    }
}
