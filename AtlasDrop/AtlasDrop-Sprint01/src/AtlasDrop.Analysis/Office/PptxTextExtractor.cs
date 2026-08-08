using System.Text;
using AtlasDrop.Core.Analysis;
using DocumentFormat.OpenXml.Packaging;
using A = DocumentFormat.OpenXml.Drawing;

namespace AtlasDrop.Analysis.Office;

public sealed class PptxTextExtractor : IPptxTextExtractor
{
    private readonly int _maxCharacters;
    private readonly int _maxSlides;

    public PptxTextExtractor(
        int maxCharacters = 500_000,
        int maxSlides = 200)
    {
        if (maxCharacters <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCharacters));

        if (maxSlides <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxSlides));

        _maxCharacters = maxCharacters;
        _maxSlides = maxSlides;
    }

    public bool CanHandle(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        return string.Equals(
            Path.GetExtension(filePath),
            ".pptx",
            StringComparison.OrdinalIgnoreCase);
    }

    public Task<TextExtractionResult> ExtractAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Le chemin PPTX est obligatoire.", nameof(filePath));

        var fullPath = Path.GetFullPath(filePath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Le fichier PPTX est introuvable.", fullPath);

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

        using var document = PresentationDocument.Open(fullPath, false);

        var presentationPart = document.PresentationPart
            ?? throw new InvalidDataException("PPTX sans présentation.");

        var slideIds = presentationPart.Presentation.SlideIdList?
            .ChildElements
            .OfType<DocumentFormat.OpenXml.Presentation.SlideId>()
            .ToArray()
            ?? Array.Empty<DocumentFormat.OpenXml.Presentation.SlideId>();

        var builder = new StringBuilder();
        var truncated = false;
        var slideNumber = 0;

        foreach (var slideId in slideIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            slideNumber++;
            if (slideNumber > _maxSlides)
            {
                truncated = true;
                break;
            }

            if (slideId.RelationshipId?.Value is null)
                continue;

            var slidePart = (SlidePart)presentationPart.GetPartById(
                slideId.RelationshipId.Value);

            AppendLimited(
                builder,
                $"[Diapositive {slideNumber}]{Environment.NewLine}",
                ref truncated);

            if (truncated)
                break;

            var texts = slidePart.Slide
                .Descendants<A.Text>()
                .Select(x => x.Text)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();

            foreach (var text in texts)
            {
                cancellationToken.ThrowIfCancellationRequested();

                AppendLimited(
                    builder,
                    text.Trim() + Environment.NewLine,
                    ref truncated);

                if (truncated)
                    break;
            }

            if (truncated)
                break;

            AppendLimited(builder, Environment.NewLine, ref truncated);
        }

        return new TextExtractionResult(
            builder.ToString().Trim(),
            "openxml-pptx",
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
