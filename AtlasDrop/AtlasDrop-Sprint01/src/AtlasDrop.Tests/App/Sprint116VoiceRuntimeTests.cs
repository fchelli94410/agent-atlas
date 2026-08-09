using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class Sprint116VoiceRuntimeTests
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
    public void Cpu_and_noavx_runtimes_are_both_packaged_for_windows_fallback()
    {
        var project = File.ReadAllText(FindFile("AtlasDrop.App.csproj"));

        Assert.Contains("Whisper.net.Runtime\" Version=\"1.9.1\" GeneratePathProperty=\"true\"", project, StringComparison.Ordinal);
        Assert.Contains("Whisper.net.Runtime.NoAvx\" Version=\"1.9.1\" GeneratePathProperty=\"true\"", project, StringComparison.Ordinal);
        Assert.Contains("PreserveWhisperNativeRuntimeLayout", project, StringComparison.Ordinal);
        Assert.Contains("$(PublishDir)runtimes", project, StringComparison.Ordinal);
        Assert.Contains("Whisper.net.Runtime native files were not found during publish", project, StringComparison.Ordinal);
    }

    [Fact]
    public void Voice_errors_shown_to_user_are_friendly_and_technical_exception_is_only_inner_exception()
    {
        var voice = File.ReadAllText(FindFile("LocalVoiceExplanationService.cs"));

        Assert.Contains("L’analyse vocale n’a pas pu démarrer. Réessaie.", voice, StringComparison.Ordinal);
        Assert.Contains("throw new InvalidOperationException(FriendlyVoiceError, ex)", voice, StringComparison.Ordinal);
        Assert.Contains("CleanupRecording()", voice, StringComparison.Ordinal);
        Assert.DoesNotContain("Native Library not found", voice, StringComparison.OrdinalIgnoreCase);
    }
}
