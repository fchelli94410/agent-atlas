using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class Sprint120MiddleClickTooltipFallbackTests
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
    public void Tooltip_overlay_falls_back_to_foreground_explorer_selection()
    {
        var code = File.ReadAllText(FindFile("App.MiddleClickTooltipFallback.cs"));

        Assert.Contains("GetForegroundWindow", code, StringComparison.Ordinal);
        Assert.Contains("TryGetSingleSelectedExplorerItem", code, StringComparison.Ordinal);
        Assert.Contains("SelectedItems()", code, StringComparison.Ordinal);
        Assert.Contains("IsPointInsideWindow", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Normal_explorer_middle_click_is_left_to_existing_activation_hook()
    {
        var code = File.ReadAllText(FindFile("App.MiddleClickTooltipFallback.cs"));

        Assert.Contains("if (IsExplorerWindow(directRoot))", code, StringComparison.Ordinal);
        Assert.Contains("return CallNextHookEx", code, StringComparison.Ordinal);
    }

    [Fact]
    public void Fallback_forwards_selected_item_through_existing_single_instance_flow()
    {
        var code = File.ReadAllText(FindFile("App.MiddleClickTooltipFallback.cs"));

        Assert.Contains("Environment.ProcessPath", code, StringComparison.Ordinal);
        Assert.Contains("ArgumentList = { selectedPath }", code, StringComparison.Ordinal);
        Assert.Contains("UseShellExecute = false", code, StringComparison.Ordinal);
    }
}
