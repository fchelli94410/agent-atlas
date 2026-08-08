namespace AtlasDrop.Core.Analysis;

public interface IPlaceDetectionService
{
    IReadOnlyList<DetectedPlace> Detect(string? text);
}
