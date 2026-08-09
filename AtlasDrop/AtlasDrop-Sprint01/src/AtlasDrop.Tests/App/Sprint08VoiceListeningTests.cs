using System.Xml.Linq;
using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class Sprint08VoiceListeningTests
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
    public void Microphone_service_publishes_a_normalized_live_audio_level()
    {
        var service = File.ReadAllText(FindFile("LocalVoiceExplanationService.cs"));

        Assert.Contains("event Action<float>? AudioLevelChanged", service, StringComparison.Ordinal);
        Assert.Contains("CalculatePeakLevel(args.Buffer, args.BytesRecorded)", service, StringComparison.Ordinal);
        Assert.Contains("Math.Clamp(peak / 32768f, 0f, 1f)", service, StringComparison.Ordinal);
        Assert.Contains("AudioLevelChanged?.Invoke(0f)", service, StringComparison.Ordinal);
    }

    [Fact]
    public void Voice_panel_shows_a_moving_meter_and_a_clear_finish_instruction()
    {
        var xaml = File.ReadAllText(FindFile("MainWindow.xaml"));
        var document = XDocument.Load(FindFile("MainWindow.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var meter = Assert.Single(
            document.Descendants(),
            element => (string?)element.Attribute(x + "Name") == "VoiceLevelMeter");

        Assert.Equal("0", (string?)meter.Attribute("Minimum"));
        Assert.Equal("1", (string?)meter.Attribute("Maximum"));
        Assert.Contains("Écoute en cours…", xaml, StringComparison.Ordinal);
        Assert.Contains("clique TERMINER pour arrêter", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"VoiceRuleActionsPanel\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Live_meter_is_connected_to_the_microphone_and_finish_button()
    {
        var finishing = File.ReadAllText(FindFile("MainWindow.Finishing.cs"));

        Assert.Contains("_voiceService.AudioLevelChanged += OnVoiceLevelChanged", finishing, StringComparison.Ordinal);
        Assert.Contains("VoiceLevelMeter.Value = Math.Clamp(level, 0f, 1f)", finishing, StringComparison.Ordinal);
        Assert.Contains("VoiceListeningVisual.Visibility = recording ? Visibility.Visible : Visibility.Collapsed", finishing, StringComparison.Ordinal);
        Assert.Contains("ExplainChoiceButton.Content = \"■ TERMINER\"", finishing, StringComparison.Ordinal);
    }
}
