namespace AtlasDrop.Core.Analysis;

public sealed record PdfOcrDecision(
    bool ShouldUseOcr,
    string Reason,
    int ExtractedCharacterCount);
