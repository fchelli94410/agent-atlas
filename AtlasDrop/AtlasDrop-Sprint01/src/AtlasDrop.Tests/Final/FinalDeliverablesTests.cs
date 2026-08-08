using Xunit;

namespace AtlasDrop.Tests.Final;

public sealed class FinalDeliverablesTests
{
    private static string FindRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(
                Path.Combine(
                    current.FullName,
                    "AtlasDrop.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "Racine solution introuvable.");
    }

    [Theory]
    [InlineData("docs", "GUIDE-UTILISATEUR.md")]
    [InlineData("docs", "INSTALLATION.md")]
    [InlineData("docs", "LIMITES-CONNUES.md")]
    [InlineData("docs", "DECISIONS.md")]
    [InlineData("docs", "SAUVEGARDE-RESTAURATION.md")]
    [InlineData("docs", "RAPPORT-FINAL.md")]
    [InlineData("docs", "CHECKLIST-VALIDATION-FINALE.md")]
    [InlineData("scripts", "SAUVEGARDER-ATLAS-DROP.cmd")]
    [InlineData("scripts", "RESTAURER-ATLAS-DROP.cmd")]
    public void Final_deliverables_exist(
        string folder,
        string fileName)
    {
        var path = Path.Combine(
            FindRoot(),
            folder,
            fileName);

        Assert.True(
            File.Exists(path),
            $"Livrable manquant : {path}");
    }

    [Fact]
    public void Solution_contains_installer_and_file_operations()
    {
        var sln = File.ReadAllText(
            Path.Combine(
                FindRoot(),
                "AtlasDrop.sln"));

        Assert.Contains(
            "AtlasDrop.Installer",
            sln,
            StringComparison.Ordinal);

        Assert.Contains(
            "AtlasDrop.FileOperations",
            sln,
            StringComparison.Ordinal);
    }
}
