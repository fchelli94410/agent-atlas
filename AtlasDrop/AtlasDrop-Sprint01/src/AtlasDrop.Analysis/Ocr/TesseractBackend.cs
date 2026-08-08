using Tesseract;

namespace AtlasDrop.Analysis.Ocr;

public sealed class TesseractBackend : ITesseractBackend
{
    public OcrResultData Recognize(
        string filePath,
        string tessDataPath,
        string languages,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var engine = new TesseractEngine(
            tessDataPath,
            languages,
            EngineMode.Default);

        cancellationToken.ThrowIfCancellationRequested();

        using var image = Pix.LoadFromFile(filePath);
        using var page = engine.Process(image, PageSegMode.Auto);

        cancellationToken.ThrowIfCancellationRequested();

        return new OcrResultData(
            page.GetText() ?? string.Empty,
            page.GetMeanConfidence());
    }
}
