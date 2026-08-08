namespace AtlasDrop.Core.Analysis;

public interface ICompanyDetectionService
{
    IReadOnlyList<DetectedCompany> Detect(string? text);
}
