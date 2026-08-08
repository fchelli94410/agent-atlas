namespace AtlasDrop.Core.Analysis;

public interface IXlsxTextExtractor
{
    bool CanHandle(string filePath);

    Task<TextExtractionResult> ExtractAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
