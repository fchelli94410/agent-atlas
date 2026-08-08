using System.Text;
using AtlasDrop.Core.Analysis;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using WordText = DocumentFormat.OpenXml.Wordprocessing.Text;

namespace AtlasDrop.Analysis.Office;

public sealed class DocxTextExtractor : IDocxTextExtractor
{
    private readonly int _maxCharacters;

    public DocxTextExtractor(int maxCharacters = 500_000)
    {
        if (maxCharacters <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCharacters));

        _maxCharacters = maxCharacters;
    }

    public bool CanHandle(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        return string.Equals(
            Path.GetExtension(filePath),
            ".docx",
            StringComparison.OrdinalIgnoreCase);
    }

    public Task<TextExtractionResult> ExtractAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Le chemin DOCX est obligatoire.", nameof(filePath));

        var fullPath = Path.GetFullPath(filePath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Le fichier DOCX est introuvable.", fullPath);

        if (!CanHandle(fullPath))
            throw new NotSupportedException(
                $"Extension non prise en charge : {Path.GetExtension(fullPath)}");

        return Task.Run(
            () => ExtractInternal(fullPath, cancellationToken),
            cancellationToken);
    }

    private TextExtractionResult ExtractInternal(
        string fullPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var document = WordprocessingDocument.Open(fullPath, false);

        var mainPart = document.MainDocumentPart
                       ?? throw new InvalidDataException("DOCX sans document principal.");

        var body = mainPart.Document.Body
                   ?? throw new InvalidDataException("DOCX sans corps de document.");

        var builder = new StringBuilder();
        var truncated = false;

        foreach (var element in body.Descendants())
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? fragment = element switch
            {
                WordText text => text.Text,
                TabChar => "\t",
                Break => Environment.NewLine,
                CarriageReturn => Environment.NewLine,
                Paragraph => Environment.NewLine,
                _ => null
            };

            if (string.IsNullOrEmpty(fragment))
                continue;

            if (builder.Length + fragment.Length > _maxCharacters)
            {
                var remaining = _maxCharacters - builder.Length;

                if (remaining > 0)
                    builder.Append(fragment.AsSpan(0, Math.Min(remaining, fragment.Length)));

                truncated = true;
                break;
            }

            builder.Append(fragment);
        }

        var textResult = NormalizeLineBreaks(builder.ToString());

        return new TextExtractionResult(
            textResult,
            "openxml-docx",
            truncated,
            new FileInfo(fullPath).Length);
    }

    private static string NormalizeLineBreaks(string value)
    {
        var lines = value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n')
            .Select(x => x.TrimEnd())
            .ToArray();

        var builder = new StringBuilder();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                if (builder.Length > 0 &&
                    !builder.ToString().EndsWith(Environment.NewLine + Environment.NewLine, StringComparison.Ordinal))
                    builder.AppendLine();

                continue;
            }

            if (builder.Length > 0 &&
                !builder.ToString().EndsWith(Environment.NewLine, StringComparison.Ordinal))
                builder.AppendLine();

            builder.Append(line);
        }

        return builder.ToString().Trim();
    }
}
