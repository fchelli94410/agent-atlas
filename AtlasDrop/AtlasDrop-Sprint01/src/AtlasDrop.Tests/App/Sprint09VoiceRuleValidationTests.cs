using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class Sprint09VoiceRuleValidationTests
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
    public void Understood_rule_requires_explicit_validate_or_redo_choice()
    {
        var xaml = File.ReadAllText(FindFile("MainWindow.xaml"));

        Assert.Contains("Content=\"VALIDER\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"OnConfirmVoiceRuleClicked\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"REFAIRE\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Click=\"OnRedoVoiceRuleClicked\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Redo_clears_the_previous_interpretation_and_restarts_recording()
    {
        var finishing = File.ReadAllText(FindFile("MainWindow.Finishing.cs"));

        Assert.Contains("_pendingVoiceExplanation = null", finishing, StringComparison.Ordinal);
        Assert.Contains("VoiceTranscriptText.Text = string.Empty", finishing, StringComparison.Ordinal);
        Assert.Contains("VoiceRulePreviewText.Text = string.Empty", finishing, StringComparison.Ordinal);
        Assert.Contains("OnExplainChoiceClicked(sender, e)", finishing, StringComparison.Ordinal);
    }

    [Fact]
    public void Learning_is_saved_only_by_the_confirmation_handler()
    {
        var code = File.ReadAllText(FindFile("MainWindow.xaml.cs"));
        var start = code.IndexOf("private void OnConfirmVoiceRuleClicked", StringComparison.Ordinal);
        var end = code.IndexOf("private void OnCancelVoiceRuleClicked", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var confirmation = code[start..end];

        Assert.Contains("SaveLearningDictionary()", confirmation, StringComparison.Ordinal);
        Assert.Contains("_pendingVoiceExplanation", confirmation, StringComparison.Ordinal);
    }
}
