using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class MainWindowNotificationTests
{
    private static string FindFile(string relativePath)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(
                current.FullName,
                "src",
                "AtlasDrop.App",
                relativePath);

            if (File.Exists(candidate))
                return candidate;

            current = current.Parent;
        }

        throw new FileNotFoundException(relativePath);
    }

    [Fact]
    public void Launch_sound_is_wired()
    {
        var code = File.ReadAllText(
            FindFile("MainWindow.xaml.cs"));

        Assert.Contains(
            "PlayLaunchSound",
            code,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Success_sound_is_wired()
    {
        var code = File.ReadAllText(
            FindFile("MainWindow.xaml.cs"));

        Assert.Contains(
            "PlaySuccessSound",
            code,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Reminder_sound_is_wired()
    {
        var code = File.ReadAllText(
            FindFile("MainWindow.xaml.cs"));

        Assert.Contains(
            "PlayReminderSound",
            code,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Text_input_marks_user_as_typing()
    {
        var code = File.ReadAllText(
            FindFile("MainWindow.xaml.cs"));

        Assert.Contains(
            "_isUserTyping = true",
            code,
            StringComparison.Ordinal);

        Assert.Contains(
            "_isUserTyping = false",
            code,
            StringComparison.Ordinal);
    }
}
