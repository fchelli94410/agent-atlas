namespace AtlasDrop.Core.Analysis;

public interface IPptxTextExtractor
{
    bool CanHandle(string filePath);

    Task<TextExtractionResult> ExtractAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
