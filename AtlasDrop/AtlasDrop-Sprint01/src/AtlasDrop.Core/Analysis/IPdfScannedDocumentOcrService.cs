namespace AtlasDrop.Core.Analysis;

public interface IPdfScannedDocumentOcrService
{
    Task<TextExtractionResult> ExtractAsync(
        string pdfPath,
        CancellationToken cancellationToken = default);
}
