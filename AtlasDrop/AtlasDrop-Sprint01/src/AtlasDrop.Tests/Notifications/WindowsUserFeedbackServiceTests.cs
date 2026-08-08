using Xunit;

namespace AtlasDrop.Tests.Notifications;

public sealed class WindowsUserFeedbackServiceTests
{
    private static string FindFile()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(
                current.FullName,
                "src",
                "AtlasDrop.Infrastructure",
                "Notifications",
                "WindowsUserFeedbackService.cs");

            if (File.Exists(candidate))
                return candidate;

            current = current.Parent;
        }

        throw new FileNotFoundException(
            "WindowsUserFeedbackService.cs introuvable.");
    }

    [Fact]
    public void Uses_native_windows_beep_without_extra_package()
    {
        var code = File.ReadAllText(FindFile());

        Assert.Contains(
            "MessageBeep",
            code,
            StringComparison.Ordinal);

        Assert.Contains(
            "user32.dll",
            code,
            StringComparison.Ordinal);

        Assert.DoesNotContain(
            "SystemSounds",
            code,
            StringComparison.Ordinal);
    }
}
