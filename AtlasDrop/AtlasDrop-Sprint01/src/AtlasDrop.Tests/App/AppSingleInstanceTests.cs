using Xunit;

namespace AtlasDrop.Tests.App;

public sealed class AppSingleInstanceTests
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
    public void App_uses_named_pipe_activation_channel()
    {
        var code = File.ReadAllText(
            FindFile("App.xaml.cs"));

        Assert.Contains(
            "NamedPipeFileActivationChannel",
            code,
            StringComparison.Ordinal);

        Assert.Contains(
            "SendToPrimaryAsync",
            code,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Secondary_instance_shuts_down()
    {
        var code = File.ReadAllText(
            FindFile("App.xaml.cs"));

        Assert.Contains(
            "if (!_activationChannel.IsPrimaryInstance)",
            code,
            StringComparison.Ordinal);

        Assert.Contains(
            "Shutdown();",
            code,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Main_window_can_receive_new_file()
    {
        var code = File.ReadAllText(
            FindFile("MainWindow.xaml.cs"));

        Assert.Contains(
            "public async void ActivateFile(string filePath)",
            code,
            StringComparison.Ordinal);

        Assert.Contains(
            "FileNameText.Text = Path.GetFileName(fullPath)",
            code,
            StringComparison.Ordinal);
    }
}
