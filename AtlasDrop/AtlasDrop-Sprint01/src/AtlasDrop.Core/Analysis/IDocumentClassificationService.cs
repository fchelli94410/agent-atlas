namespace AtlasDrop.Core.Analysis;

public interface IDocumentClassificationService
{
    DocumentClassificationResult Classify(
        string fileName,
        string? extractedText = null);
}
