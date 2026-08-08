namespace AtlasDrop.Core.Analysis;

public interface IMsgExtractor
{
    bool CanHandle(string filePath);

    Task<MsgAnalysisResult> ExtractAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
