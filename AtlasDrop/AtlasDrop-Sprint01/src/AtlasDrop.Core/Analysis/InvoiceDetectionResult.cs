namespace AtlasDrop.Core.Analysis;

public sealed record InvoiceDetectionResult(
    bool IsLikelyInvoice,
    double Confidence,
    string? InvoiceNumber,
    IReadOnlyList<DetectedAmount> Amounts,
    IReadOnlyList<string> Signals);
