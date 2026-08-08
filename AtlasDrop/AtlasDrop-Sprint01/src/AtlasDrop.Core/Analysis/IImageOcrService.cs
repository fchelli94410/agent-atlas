namespace AtlasDrop.Core.Analysis;

public interface IImageOcrService
{
    bool CanHandle(string filePath);

    Task<OcrResult> RecognizeAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
