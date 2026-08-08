namespace AtlasDrop.Analysis.Ocr;

public interface ITesseractBackend
{
    OcrResultData Recognize(
        string filePath,
        string tessDataPath,
        string languages,
        CancellationToken cancellationToken);
}

public sealed record OcrResultData(
    string Text,
    float Confidence);
