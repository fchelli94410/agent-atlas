namespace AtlasDrop.Core.Analysis;

public interface IDocxTextExtractor
{
    bool CanHandle(string filePath);

    Task<TextExtractionResult> ExtractAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
