namespace AtlasDrop.Core.Analysis;

public interface IDateDetectionService
{
    IReadOnlyList<DetectedDate> Detect(string? text);
}
