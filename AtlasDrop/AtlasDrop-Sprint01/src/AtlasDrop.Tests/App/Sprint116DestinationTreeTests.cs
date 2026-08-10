using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class Sprint116DestinationTreeTests
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
    public void Final_destination_folder_is_emphasized_without_hiding_the_parent_path()
    {
        var code = File.ReadAllText(FindFile("MainWindow.Finishing.cs"));

        Assert.Contains("HighlightDestinationLeaf()", code, StringComparison.Ordinal);
        Assert.Contains("LastIndexOf(separator", code, StringComparison.Ordinal);
        Assert.Contains("FontWeights.ExtraBold", code, StringComparison.Ordinal);
        Assert.Contains("Color.FromRgb(220, 252, 231)", code, StringComparison.Ordinal);
        Assert.Contains("ProposedPathText.Inlines.Add(new Run(prefix))", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Full_tree_remains_navigable_and_only_proposed_path_is_auto_expanded()
    {
        var code = File.ReadAllText(FindFile("MainWindow.Finishing.cs"));

        Assert.Contains("AllowedRootFolderNames.All(visibleRoots.Contains)", code, StringComparison.Ordinal);
        Assert.Contains("foreach (var folder in _folders", code, StringComparison.Ordinal);
        Assert.Contains("node.IsExpanded = !string.IsNullOrWhiteSpace(_proposedFolder)", code, StringComparison.Ordinal);
        Assert.Contains("IsSameOrChild(_proposedFolder, folder.Path)", code, StringComparison.Ordinal);
        Assert.Contains("proposedNode.BringIntoView()", code, StringComparison.Ordinal);
    }
}
