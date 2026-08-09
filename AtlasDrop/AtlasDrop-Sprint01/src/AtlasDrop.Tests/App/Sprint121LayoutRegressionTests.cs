using System.Globalization;
using System.Xml.Linq;
using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class Sprint121LayoutRegressionTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XDocument ReadMainWindowXaml()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "AtlasDrop.App", "MainWindow.xaml");
            if (File.Exists(candidate)) return XDocument.Load(candidate, LoadOptions.PreserveWhitespace);
            current = current.Parent;
        }

        throw new FileNotFoundException("MainWindow.xaml");
    }

    private static XElement Named(XDocument document, string name) =>
        document.Descendants().Single(element =>
            string.Equals((string?)element.Attribute(Xaml + "Name"), name, StringComparison.Ordinal));

    private static double Number(XElement element, string attribute) =>
        double.Parse((string?)element.Attribute(attribute) ?? throw new InvalidDataException(attribute),
            CultureInfo.InvariantCulture);

    private static double[] Thickness(XElement element, string attribute)
    {
        var raw = (string?)element.Attribute(attribute) ?? throw new InvalidDataException(attribute);
        return raw.Split(',').Select(value => double.Parse(value, CultureInfo.InvariantCulture)).ToArray();
    }

    [Fact]
    public void Window_is_taller_and_tree_is_at_least_thirty_three_percent_taller_than_green_baseline()
    {
        var document = ReadMainWindowXaml();
        var window = document.Root ?? throw new InvalidDataException("Window");
        var treeScroll = Named(document, "MainContentScrollViewer");

        Assert.True(Number(window, "Height") >= 800d);
        Assert.True(Number(treeScroll, "MinHeight") >= 220d * 1.33d);
    }

    [Fact]
    public void Proposed_folder_information_is_compact()
    {
        var document = ReadMainWindowXaml();
        var confidence = Named(document, "ConfidenceInfoPanel");
        var padding = Thickness(confidence, "Padding");

        Assert.Equal(4, padding.Length);
        Assert.True(padding[1] + padding[3] <= 6d,
            "Le bloc de confiance doit rester sensiblement plus fin que la base verte.");
    }

    [Fact]
    public void Rename_panel_has_a_complete_visible_frame()
    {
        var document = ReadMainWindowXaml();
        var rename = Named(document, "RenamePanel");

        Assert.Equal("White", (string?)rename.Attribute("Background"));
        Assert.False(string.IsNullOrWhiteSpace((string?)rename.Attribute("BorderBrush")));
        Assert.True(Number(rename, "BorderThickness") >= 1.5d);
        Assert.False(string.IsNullOrWhiteSpace((string?)rename.Attribute("CornerRadius")));
    }

    [Fact]
    public void Idle_validation_instruction_can_never_be_visible()
    {
        var document = ReadMainWindowXaml();
        var status = Named(document, "StatusText");
        var container = status.Parent ?? throw new InvalidDataException("StatusText parent");

        var hidingTrigger = container.Descendants()
            .Where(element => element.Name.LocalName is "DataTrigger" or "Trigger")
            .SingleOrDefault(element => string.Equals(
                (string?)element.Attribute("Value"),
                "Valide explicitement avant tout déplacement.",
                StringComparison.Ordinal));

        Assert.NotNull(hidingTrigger);
        Assert.Contains(hidingTrigger!.Descendants(), element =>
            element.Name.LocalName == "Setter" &&
            string.Equals((string?)element.Attribute("Property"), "Visibility", StringComparison.Ordinal) &&
            string.Equals((string?)element.Attribute("Value"), "Collapsed", StringComparison.Ordinal));
    }

    [Fact]
    public void Learning_controls_are_the_last_footer_block_and_are_bottom_aligned()
    {
        var document = ReadMainWindowXaml();
        var learning = Named(document, "LearningControlsPanel");
        var footer = learning.Parent ?? throw new InvalidDataException("LearningControlsPanel parent");

        Assert.Equal("StackPanel", footer.Name.LocalName);
        Assert.Equal("3", (string?)footer.Attribute("Grid.Row"));
        Assert.Equal("Bottom", (string?)footer.Attribute("VerticalAlignment"));
        Assert.Same(learning, footer.Elements().Last());
        Assert.Equal("Bottom", (string?)learning.Attribute("VerticalAlignment"));
    }

    [Fact]
    public void Footer_has_no_large_artificial_spacing()
    {
        var document = ReadMainWindowXaml();
        var learning = Named(document, "LearningControlsPanel");
        var footer = learning.Parent ?? throw new InvalidDataException("footer");
        var footerMargin = Thickness(footer, "Margin");
        var decisions = Named(document, "DecisionButtons");
        var decisionMargin = Thickness(decisions, "Margin");

        Assert.Equal(4, footerMargin.Length);
        Assert.True(footerMargin[1] <= 1d && footerMargin[3] <= 1d);
        Assert.Equal(4, decisionMargin.Length);
        Assert.True(decisionMargin[3] <= 1d);
    }
}
