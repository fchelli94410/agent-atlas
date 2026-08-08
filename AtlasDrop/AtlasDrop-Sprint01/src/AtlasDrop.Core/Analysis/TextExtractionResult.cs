namespace AtlasDrop.Core.Analysis;

public sealed record TextExtractionResult(
    string Text,
    string DetectedEncoding,
    bool WasTruncated,
    long BytesRead);
