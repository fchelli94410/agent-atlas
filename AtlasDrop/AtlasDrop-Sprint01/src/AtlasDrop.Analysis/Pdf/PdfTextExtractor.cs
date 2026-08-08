using System.Text;
using AtlasDrop.Core.Analysis;
using UglyToad.PdfPig;

namespace AtlasDrop.Analysis.Pdf;

public sealed class PdfTextExtractor : IPdfTextExtractor
{
    private readonly int _maxCharacters;
    private readonly int _maxPages;

    public PdfTextExtractor(
        int maxCharacters = 500_000,
        int maxPages = 100)
    {
        if (maxCharacters <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCharacters));

        if (maxPages <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxPages));

        _maxCharacters = maxCharacters;
        _maxPages = maxPages;
    }

    public bool CanHandle(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        return string.Equals(
            Path.GetExtension(filePath),
            ".pdf",
            StringComparison.OrdinalIgnoreCase);
    }

    public Task<TextExtractionResult> ExtractAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Le chemin PDF est obligatoire.", nameof(filePath));

        var fullPath = Path.GetFullPath(filePath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Le fichier PDF est introuvable.", fullPath);

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

        using var document = PdfDocument.Open(fullPath);

        var builder = new StringBuilder();
        var truncated = false;
        var pageCount = 0;

        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();

            pageCount++;
            if (pageCount > _maxPages)
            {
                truncated = true;
                break;
            }

            var text = page.Text ?? string.Empty;

            if (builder.Length > 0)
                AppendLimited(builder, Environment.NewLine + Environment.NewLine, ref truncated);

            AppendLimited(
                builder,
                $"[Page {pageCount}]{Environment.NewLine}",
                ref truncated);

            if (truncated)
                break;

            AppendLimited(builder, text.Trim(), ref truncated);

            if (truncated)
                break;
        }

        return new TextExtractionResult(
            builder.ToString().Trim(),
            "pdf-text",
            truncated,
            new FileInfo(fullPath).Length);
    }

    private void AppendLimited(
        StringBuilder builder,
        string value,
        ref bool truncated)
    {
        if (truncated || string.IsNullOrEmpty(value))
            return;

        var remaining = _maxCharacters - builder.Length;

        if (remaining <= 0)
        {
            truncated = true;
            return;
        }

        if (value.Length <= remaining)
        {
            builder.Append(value);
            return;
        }

        builder.Append(value.AsSpan(0, remaining));
        truncated = true;
    }
}
