namespace AtlasDrop.Core.Analysis;

public interface ITextFileExtractor
{
    bool CanHandle(string filePath);

    Task<TextExtractionResult> ExtractAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
