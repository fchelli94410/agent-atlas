namespace AtlasDrop.Core.Analysis;

public sealed record OcrResult(
    string Text,
    float Confidence,
    string Languages,
    bool WasTruncated);
