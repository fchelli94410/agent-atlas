using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class AutomaticSuggestionQualityTests
{
    private static string FindFile()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(
                current.FullName,
                "src",
                "AtlasDrop.App",
                "MainWindow.xaml.cs");

            if (File.Exists(candidate))
                return candidate;

            current = current.Parent;
        }

        throw new FileNotFoundException("MainWindow.xaml.cs");
    }

    [Fact]
    public void Generic_document_term_is_not_used_for_unknown_type()
    {
        var code = File.ReadAllText(FindFile());

        Assert.Contains(
            "_classification.Type != DocumentType.Unknown",
            code,
            StringComparison.Ordinal);

        Assert.Contains(
            "\"Document\"",
            code,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Weak_automatic_results_are_filtered()
    {
        var code = File.ReadAllText(FindFile());

        Assert.Contains(
            ".Where(x => x.Score >= 0.30)",
            code,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Suggestions_are_deduplicated_by_full_path()
    {
        var code = File.ReadAllText(FindFile());

        Assert.Contains(
            ".GroupBy(",
            code,
            StringComparison.Ordinal);

        Assert.Contains(
            "x => x.FullPath",
            code,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Place_is_cleaned_when_company_leaks_into_it()
    {
        var code = File.ReadAllText(FindFile());

        Assert.Contains(
            "CleanDetectedPlace",
            code,
            StringComparison.Ordinal);

        Assert.Contains(
            "cleaned.IndexOf(",
            code,
            StringComparison.Ordinal);
    }
    [Fact]
    public void Ranking_uses_distinctive_hierarchical_and_calibrated_signals()
    {
        var code = File.ReadAllText(FindFile());

        Assert.Contains("GetDistinctiveTokenBoost", code, StringComparison.Ordinal);
        Assert.Contains("inverseFrequency", code, StringComparison.Ordinal);
        Assert.Contains("GetHierarchyBoost", code, StringComparison.Ordinal);
        Assert.Contains("GetCalibratedConfidence", code, StringComparison.Ordinal);
        Assert.Contains("runnerUpScore", code, StringComparison.Ordinal);
        Assert.Contains("ambiguityPenalty", code, StringComparison.Ordinal);
        Assert.Contains(".Take(2)", code, StringComparison.Ordinal);
    }

}
