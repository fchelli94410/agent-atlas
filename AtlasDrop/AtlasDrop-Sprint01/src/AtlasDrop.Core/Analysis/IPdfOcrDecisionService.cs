namespace AtlasDrop.Core.Analysis;

public interface IPdfOcrDecisionService
{
    PdfOcrDecision Decide(string? extractedText);
}
