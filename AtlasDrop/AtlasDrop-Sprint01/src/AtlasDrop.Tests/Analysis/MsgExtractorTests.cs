using Xunit;
using AtlasDrop.Analysis.Msg;

namespace AtlasDrop.Tests.Analysis;

public sealed class MsgExtractorTests
{
    [Theory]
    [InlineData("mail.msg", true)]
    [InlineData("MAIL.MSG", true)]
    [InlineData("mail.eml", false)]
    [InlineData("mail.pdf", false)]
    public void CanHandle_only_msg(string fileName, bool expected)
    {
        var extractor = new MsgExtractor(new FakeBackend());
        Assert.Equal(expected, extractor.CanHandle(fileName));
    }

    [Fact]
    public async Task Extracts_core_metadata_and_body()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "mail.msg");
            await File.WriteAllBytesAsync(file, new byte[] { 1, 2, 3 });

            var sent = new DateTime(2026, 8, 7, 12, 30, 0, DateTimeKind.Local);

            var extractor = new MsgExtractor(
                new FakeBackend(new MsgBackendResult(
                    "Facture EDF",
                    "contact@edf.fr",
                    "franck@example.com",
                    "compta@example.com",
                    sent,
                    "Facture Courbevoie 125,50 EUR",
                    null)));

            var result = await extractor.ExtractAsync(file);

            Assert.Equal("Facture EDF", result.Subject);
            Assert.Equal("contact@edf.fr", result.Sender);
            Assert.Equal("franck@example.com", result.RecipientsTo);
            Assert.Equal("compta@example.com", result.RecipientsCc);
            Assert.Equal(sent, result.SentOn);
            Assert.Contains("Courbevoie", result.BodyText);
            Assert.False(result.WasTruncated);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Falls_back_to_html_body()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "mail.msg");
            await File.WriteAllBytesAsync(file, new byte[] { 1 });

            var extractor = new MsgExtractor(
                new FakeBackend(new MsgBackendResult(
                    "Sujet",
                    "A",
                    "B",
                    null,
                    null,
                    null,
                    "<p>Bonjour <b>Courbevoie</b></p><p>Montant 120 &euro;</p>")));

            var result = await extractor.ExtractAsync(file);

            Assert.Contains("Bonjour", result.BodyText);
            Assert.Contains("Courbevoie", result.BodyText);
            Assert.DoesNotContain("<b>", result.BodyText);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Body_limit_is_respected()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "mail.msg");
            await File.WriteAllBytesAsync(file, new byte[] { 1 });

            var extractor = new MsgExtractor(
                new FakeBackend(new MsgBackendResult(
                    null, null, null, null, null,
                    new string('X', 1000),
                    null)),
                maxBodyCharacters: 100);

            var result = await extractor.ExtractAsync(file);

            Assert.True(result.WasTruncated);
            Assert.Equal(100, result.BodyText.Length);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Missing_msg_is_rejected()
    {
        var missing = Path.Combine(
            Path.GetTempPath(),
            "AtlasDrop.Tests",
            Guid.NewGuid().ToString("N"),
            "missing.msg");

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => new MsgExtractor(new FakeBackend()).ExtractAsync(missing));
    }

    [Fact]
    public async Task Unsupported_extension_is_rejected()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "mail.txt");
            await File.WriteAllTextAsync(file, "fake");

            await Assert.ThrowsAsync<NotSupportedException>(
                () => new MsgExtractor(new FakeBackend()).ExtractAsync(file));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Cancellation_is_respected()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "mail.msg");
            await File.WriteAllBytesAsync(file, new byte[] { 1 });

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => new MsgExtractor(new FakeBackend()).ExtractAsync(file, cts.Token));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Source_msg_is_never_modified()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "mail.msg");
            await File.WriteAllBytesAsync(file, new byte[] { 1, 2, 3, 4 });

            var beforeLength = new FileInfo(file).Length;
            var beforeWrite = File.GetLastWriteTimeUtc(file);

            _ = await new MsgExtractor(new FakeBackend()).ExtractAsync(file);

            Assert.Equal(beforeLength, new FileInfo(file).Length);
            Assert.Equal(beforeWrite, File.GetLastWriteTimeUtc(file));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    private sealed class FakeBackend : IMsgReaderBackend
    {
        private readonly MsgBackendResult _result;

        public FakeBackend(MsgBackendResult? result = null)
        {
            _result = result ?? new MsgBackendResult(
                "Sujet",
                "Expediteur",
                "Destinataire",
                null,
                DateTime.Now,
                "Corps",
                null);
        }

        public MsgBackendResult Read(
            string filePath,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return _result;
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "AtlasDrop.Tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteTree(string path)
    {
        if (!Directory.Exists(path))
            return;

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }
}
