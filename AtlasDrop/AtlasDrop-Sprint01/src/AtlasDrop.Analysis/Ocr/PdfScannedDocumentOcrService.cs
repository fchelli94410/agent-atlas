using System.Text;
using AtlasDrop.Core.Analysis;

namespace AtlasDrop.Analysis.Ocr;

public sealed class PdfScannedDocumentOcrService : IPdfScannedDocumentOcrService
{
    private readonly IPdfPageImageRenderer _renderer;
    private readonly IImageOcrService _imageOcr;
    private readonly int _maxPages;
    private readonly int _maxCharacters;

    public PdfScannedDocumentOcrService(
        IPdfPageImageRenderer renderer,
        IImageOcrService imageOcr,
        int maxPages = 5,
        int maxCharacters = 250_000)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _imageOcr = imageOcr ?? throw new ArgumentNullException(nameof(imageOcr));

        if (maxPages <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxPages));

        if (maxCharacters <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCharacters));

        _maxPages = maxPages;
        _maxCharacters = maxCharacters;
    }

    public async Task<TextExtractionResult> ExtractAsync(
        string pdfPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pdfPath))
            throw new ArgumentException("Le chemin PDF est obligatoire.", nameof(pdfPath));

        var fullPath = Path.GetFullPath(pdfPath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Le fichier PDF est introuvable.", fullPath);

        if (!string.Equals(Path.GetExtension(fullPath), ".pdf", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Seuls les fichiers PDF sont pris en charge.");

        var tempRoot = Path.Combine(
            Path.GetTempPath(),
            "AtlasDrop.Ocr",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(tempRoot);

        try
        {
            var rendered = await _renderer.RenderPagesAsync(
                fullPath,
                tempRoot,
                _maxPages,
                cancellationToken);

            var builder = new StringBuilder();
            var truncated = false;
            var pageNumber = 0;

            foreach (var imagePath in rendered.Take(_maxPages))
            {
                cancellationToken.ThrowIfCancellationRequested();

                pageNumber++;
                var result = await _imageOcr.RecognizeAsync(imagePath, cancellationToken);

                var fragment = $"[Page OCR {pageNumber}]{Environment.NewLine}{result.Text.Trim()}";

                if (builder.Length > 0)
                    fragment = Environment.NewLine + Environment.NewLine + fragment;

                var remaining = _maxCharacters - builder.Length;

                if (remaining <= 0)
                {
                    truncated = true;
                    break;
                }

                if (fragment.Length > remaining)
                {
                    builder.Append(fragment.AsSpan(0, remaining));
                    truncated = true;
                    break;
                }

                builder.Append(fragment);

                if (result.WasTruncated)
                    truncated = true;
            }

            return new TextExtractionResult(
                builder.ToString().Trim(),
                "pdf-ocr",
                truncated,
                new FileInfo(fullPath).Length);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
                // Dossier temporaire uniquement.
            }
        }
    }
}
