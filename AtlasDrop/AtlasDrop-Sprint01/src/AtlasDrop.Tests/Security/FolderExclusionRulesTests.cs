using Xunit;
using AtlasDrop.Core.Configuration;
using AtlasDrop.Core.Security;

namespace AtlasDrop.Tests.Security;

public sealed class FolderExclusionRulesTests
{
    private static FolderExclusionRules Rules() => new(new AtlasDropOptions());

    [Theory]
    [InlineData("Bureau")]
    [InlineData("Documents")]
    [InlineData("Images")]
    [InlineData("99 - Archives")]
    [InlineData("pour voir")]
    [InlineData(".cache")]
    [InlineData("$temp")]
    [InlineData("Temp")]
    [InlineData("tmp")]
    [InlineData("Provisoire")]
    [InlineData("cache")]
    [InlineData("thumbnail")]
    [InlineData("$Recycle.Bin")]
    [InlineData("Corbeille")]
    [InlineData("System Volume Information")]
    [InlineData("node_modules")]
    [InlineData("bin")]
    [InlineData("obj")]
    public void Known_excluded_names_are_rejected(string name)
    {
        Assert.True(Rules().IsExcludedName(name));
    }

    [Theory]
    [InlineData("01 - Immobilier")]
    [InlineData("Courbevoie")]
    [InlineData("Factures")]
    [InlineData("2026")]
    public void Normal_names_are_allowed(string name)
    {
        Assert.False(Rules().IsExcludedName(name));
    }

    [Fact]
    public void Nested_archive_is_excluded()
    {
        var rules = Rules();
        var root = @"C:\Users\fchelli\OneDrive - ALTEDIS";
        var path = root + @"\01 - Immobilier\Paris\99 - Archives\Factures";

        Assert.True(rules.IsExcludedPath(root, path));
    }

    [Fact]
    public void Path_outside_root_is_excluded()
    {
        var rules = Rules();

        Assert.True(rules.IsExcludedPath(
            @"C:\Users\fchelli\OneDrive - ALTEDIS",
            @"C:\Users\fchelli\Downloads"));
    }

    [Fact]
    public void Normal_path_is_not_excluded()
    {
        var rules = Rules();
        var root = @"C:\Users\fchelli\OneDrive - ALTEDIS";

        Assert.False(rules.IsExcludedPath(
            root,
            root + @"\01 - Immobilier\Courbevoie"));
    }

    [Fact]
    public void User_added_exclusion_is_respected()
    {
        var rules = new FolderExclusionRules(new AtlasDropOptions
        {
            ExcludedFolderNames = new[]
            {
                "Bureau",
                "Documents",
                "Images",
                "99 - Archives",
                "pour voir",
                "MON DOSSIER INTERDIT"
            }
        });

        Assert.True(rules.IsExcludedName("mon dossier interdit"));
    }
}
