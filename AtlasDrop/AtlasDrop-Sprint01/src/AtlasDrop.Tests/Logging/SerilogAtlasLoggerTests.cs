using Xunit;
using AtlasDrop.Infrastructure.Logging;

namespace AtlasDrop.Tests.Logging;

public sealed class SerilogAtlasLoggerTests : IDisposable
{
    private readonly string _root;

    public SerilogAtlasLoggerTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "AtlasDropLoggingTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void Information_creates_log_file()
    {
        using (var logger = new SerilogAtlasLogger(_root))
        {
            logger.Information(
                "TestEvent",
                "Message de test");
        }

        var files = Directory.GetFiles(
            _root,
            "atlasdrop-*.log");

        Assert.NotEmpty(files);
    }

    [Fact]
    public void Multiline_messages_are_flattened()
    {
        using (var logger = new SerilogAtlasLogger(_root))
        {
            logger.Warning(
                "TestEvent",
                "ligne1\r\nligne2");
        }

        var file = Assert.Single(
            Directory.GetFiles(
                _root,
                "atlasdrop-*.log"));

        var content = File.ReadAllText(file);

        Assert.DoesNotContain(
            "ligne1\r\nligne2",
            content,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Very_long_message_is_truncated()
    {
        using (var logger = new SerilogAtlasLogger(_root))
        {
            logger.Information(
                "TestEvent",
                new string('A', 2000));
        }

        var file = Assert.Single(
            Directory.GetFiles(
                _root,
                "atlasdrop-*.log"));

        var content = File.ReadAllText(file);

        Assert.DoesNotContain(
            new string('A', 1000),
            content,
            StringComparison.Ordinal);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
        }
    }
}
