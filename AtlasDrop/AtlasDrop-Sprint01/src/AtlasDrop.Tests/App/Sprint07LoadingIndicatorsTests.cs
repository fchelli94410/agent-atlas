using System.Xml.Linq;
using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class Sprint07LoadingIndicatorsTests
{
    private static string FindMainWindow()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "AtlasDrop.App", "MainWindow.xaml");
            if (File.Exists(candidate)) return candidate;
            current = current.Parent;
        }

        throw new FileNotFoundException("MainWindow.xaml introuvable.");
    }

    [Fact]
    public void Search_displays_a_discreet_indeterminate_progress_indicator()
    {
        var xaml = File.ReadAllText(FindMainWindow());
        var document = XDocument.Load(FindMainWindow());
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var progress = Assert.Single(
            document.Descendants(),
            element => (string?)element.Attribute(x + "Name") == "SearchLoadingProgress");

        Assert.Equal("True", (string?)progress.Attribute("IsIndeterminate"));
        Assert.Contains("Recherche du meilleur emplacement…", xaml, StringComparison.Ordinal);
        Assert.Contains("Value=\"Analyse en cours…\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Value=\"Actualisation des dossiers et nouvelle analyse…\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Explorer_loading_has_its_own_discreet_progress_indicator()
    {
        var xaml = File.ReadAllText(FindMainWindow());
        var document = XDocument.Load(FindMainWindow());
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var progress = Assert.Single(
            document.Descendants(),
            element => (string?)element.Attribute(x + "Name") == "ExplorerLoadingProgress");

        Assert.Equal("True", (string?)progress.Attribute("IsIndeterminate"));
        Assert.Contains("Value=\"● Ouverture de l’Explorateur…\"", xaml, StringComparison.Ordinal);
    }
}
