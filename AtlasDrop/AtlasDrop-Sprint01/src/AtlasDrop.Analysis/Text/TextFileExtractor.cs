using System.Text;
using AtlasDrop.Core.Analysis;

namespace AtlasDrop.Analysis.Text;

public sealed class TextFileExtractor : ITextFileExtractor
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt",
            ".csv"
        };

    private readonly int _maxBytes;

    static TextFileExtractor()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public TextFileExtractor(int maxBytes = 2 * 1024 * 1024)
    {
        if (maxBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxBytes));

        _maxBytes = maxBytes;
    }

    public bool CanHandle(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        var extension = Path.GetExtension(filePath);
        return SupportedExtensions.Contains(extension);
    }

    public async Task<TextExtractionResult> ExtractAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Le chemin du fichier est obligatoire.", nameof(filePath));

        var fullPath = Path.GetFullPath(filePath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Le fichier texte est introuvable.", fullPath);

        if (!CanHandle(fullPath))
            throw new NotSupportedException(
                $"Extension non prise en charge : {Path.GetExtension(fullPath)}");

        await using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 64 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        var bytesToRead = (int)Math.Min(stream.Length, _maxBytes);
        var buffer = new byte[bytesToRead];

        var totalRead = 0;
        while (totalRead < bytesToRead)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var read = await stream.ReadAsync(
                buffer.AsMemory(totalRead, bytesToRead - totalRead),
                cancellationToken);

            if (read == 0)
                break;

            totalRead += read;
        }

        var data = buffer.AsSpan(0, totalRead).ToArray();
        var (encoding, preambleLength) = DetectEncoding(data);

        var text = encoding.GetString(
            data,
            Math.Min(preambleLength, data.Length),
            Math.Max(0, data.Length - preambleLength));

        return new TextExtractionResult(
            text,
            encoding.WebName,
            stream.Length > _maxBytes,
            totalRead);
    }

    private static (Encoding Encoding, int PreambleLength) DetectEncoding(byte[] data)
    {
        if (data.Length >= 3 &&
            data[0] == 0xEF &&
            data[1] == 0xBB &&
            data[2] == 0xBF)
            return (new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), 3);

        if (data.Length >= 2 &&
            data[0] == 0xFF &&
            data[1] == 0xFE)
            return (Encoding.Unicode, 2);

        if (data.Length >= 2 &&
            data[0] == 0xFE &&
            data[1] == 0xFF)
            return (Encoding.BigEndianUnicode, 2);

        try
        {
            var strictUtf8 = new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true);

            _ = strictUtf8.GetString(data);
            return (strictUtf8, 0);
        }
        catch (DecoderFallbackException)
        {
            // Très fréquent dans les anciens CSV Windows français.
            return (Encoding.GetEncoding(1252), 0);
        }
    }
}
