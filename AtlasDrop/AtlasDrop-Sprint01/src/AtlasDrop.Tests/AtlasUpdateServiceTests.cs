using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AtlasDrop.Core.Logging;
using AtlasDrop.Infrastructure.Updates;

namespace AtlasDrop.Tests;

public sealed class AtlasUpdateServiceTests
{
    private static readonly Uri ManifestUri =
        new("https://updates.example/manifest.json");

    [Fact]
    public async Task Same_version_does_not_download_installer()
    {
        var part = new byte[] { 1 };
        var handler = new MemoryHttpHandler();
        handler.Add(
            ManifestUri,
            BuildManifest(
                "1.0.9",
                [part],
                ["chunks/part-00"]));

        using var client = new HttpClient(handler);
        var directory = CreateTemporaryDirectory();

        try
        {
            var service = new AtlasUpdateService(
                client,
                new TestLogger(),
                ManifestUri,
                directory);

            var result = await service.PrepareUpdateAsync(
                new Version(1, 0, 9));

            Assert.Equal(
                UpdatePreparationStatus.NoUpdate,
                result.Status);
            Assert.Null(result.InstallerPath);
            Assert.Empty(
                Directory.EnumerateFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task New_version_is_reassembled_and_verified()
    {
        var first = Encoding.UTF8.GetBytes("atlas-");
        var second = Encoding.UTF8.GetBytes("drop-update");
        var expected = first.Concat(second).ToArray();

        var handler = new MemoryHttpHandler();
        handler.Add(
            ManifestUri,
            BuildManifest(
                "1.0.10",
                [first, second],
                ["chunks/part-00", "chunks/part-01"]));
        handler.Add(
            new Uri(ManifestUri, "chunks/part-00"),
            first);
        handler.Add(
            new Uri(ManifestUri, "chunks/part-01"),
            second);

        using var client = new HttpClient(handler);
        var directory = CreateTemporaryDirectory();

        try
        {
            var service = new AtlasUpdateService(
                client,
                new TestLogger(),
                ManifestUri,
                directory);

            var result = await service.PrepareUpdateAsync(
                new Version(1, 0, 9));

            Assert.Equal(
                UpdatePreparationStatus.Ready,
                result.Status);
            Assert.NotNull(result.InstallerPath);
            Assert.True(File.Exists(result.InstallerPath));
            Assert.Equal(
                expected,
                await File.ReadAllBytesAsync(
                    result.InstallerPath));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task Corrupted_part_is_rejected_and_removed()
    {
        var expectedPart =
            Encoding.UTF8.GetBytes("trusted");
        var corruptedPart =
            Encoding.UTF8.GetBytes("changed");

        var handler = new MemoryHttpHandler();
        handler.Add(
            ManifestUri,
            BuildManifest(
                "1.0.10",
                [expectedPart],
                ["chunks/part-00"]));
        handler.Add(
            new Uri(ManifestUri, "chunks/part-00"),
            corruptedPart);

        using var client = new HttpClient(handler);
        var directory = CreateTemporaryDirectory();

        try
        {
            var service = new AtlasUpdateService(
                client,
                new TestLogger(),
                ManifestUri,
                directory);

            var result = await service.PrepareUpdateAsync(
                new Version(1, 0, 9));

            Assert.Equal(
                UpdatePreparationStatus.Failed,
                result.Status);
            Assert.Empty(
                Directory.EnumerateFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static byte[] BuildManifest(
        string version,
        IReadOnlyList<byte[]> parts,
        IReadOnlyList<string> names)
    {
        var installer = parts
            .SelectMany(part => part)
            .ToArray();

        var manifest = new UpdateManifest
        {
            SchemaVersion = 1,
            Product = "Atlas Drop",
            Version = version,
            Platform = "windows-x64",
            Installer = "Installer-Atlas-Drop.exe",
            Size = installer.LongLength,
            Sha256 = Sha256(installer),
            Parts = parts
                .Select((part, index) => new UpdatePart
                {
                    File = names[index],
                    Size = part.LongLength,
                    Sha256 = Sha256(part)
                })
                .ToList()
        };

        return Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(manifest));
    }

    private static string Sha256(
        byte[] content)
    {
        return Convert.ToHexString(
            SHA256.HashData(content))
            .ToLowerInvariant();
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "AtlasDrop.Tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class MemoryHttpHandler : HttpMessageHandler
    {
        private readonly Dictionary<Uri, byte[]> _responses = [];

        public void Add(
            Uri uri,
            byte[] content)
        {
            _responses[uri] = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (request.RequestUri is not null &&
                _responses.TryGetValue(
                    request.RequestUri,
                    out var content))
            {
                return Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        RequestMessage = request,
                        Content = new ByteArrayContent(content)
                    });
            }

            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    RequestMessage = request
                });
        }
    }

    private sealed class TestLogger : IAtlasLogger
    {
        public void Information(
            string eventName,
            string message)
        {
        }

        public void Warning(
            string eventName,
            string message)
        {
        }

        public void Error(
            string eventName,
            string message,
            Exception? exception = null)
        {
        }
    }
}
