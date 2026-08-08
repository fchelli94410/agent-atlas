using Xunit;
using AtlasDrop.Core.Integration;
using AtlasDrop.Installer;

namespace AtlasDrop.Tests.Installer;

public sealed class InstallerEngineTests : IDisposable
{
    private readonly string _root;

    public InstallerEngineTests()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "AtlasDropInstallerTests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_root);
    }

    [Fact]
    public void Self_test_rejects_missing_payload()
    {
        Assert.False(
            InstallerEngine.SelfTestPayload(
                Path.Combine(_root, "missing")));
    }

    [Fact]
    public void Self_test_accepts_app_exe_and_dll_payload()
    {
        var payload = CreatePayload();

        Assert.True(
            InstallerEngine.SelfTestPayload(payload));
    }

    [Fact]
    public void Install_rejects_incomplete_payload_before_registry()
    {
        var fakeMenu = new FakeContextMenu();
        var engine = new InstallerEngine(fakeMenu);

        var result = engine.Install(_root);

        Assert.False(result.Success);
        Assert.False(fakeMenu.RegisterCalled);
    }

    private string CreatePayload()
    {
        var payload = Path.Combine(_root, "payload");
        Directory.CreateDirectory(payload);

        File.WriteAllText(
            Path.Combine(payload, "AtlasDrop.App.exe"),
            "fake");

        File.WriteAllText(
            Path.Combine(payload, "AtlasDrop.Core.dll"),
            "fake");

        return payload;
    }

    private sealed class FakeContextMenu
        : IWindowsContextMenuService
    {
        public bool RegisterCalled { get; private set; }

        public ContextMenuRegistrationResult Register(
            string executablePath)
        {
            RegisterCalled = true;

            return new ContextMenuRegistrationResult(
                true,
                "ok");
        }

        public ContextMenuRegistrationResult Unregister()
        {
            return new ContextMenuRegistrationResult(
                true,
                "ok");
        }
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
