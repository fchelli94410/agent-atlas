namespace AtlasDrop.Core.Analysis;

public interface IPdfTextExtractor
{
    bool CanHandle(string filePath);

    Task<TextExtractionResult> ExtractAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
