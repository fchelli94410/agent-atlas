namespace AtlasDrop.Core.Analysis;

public interface IPersonAndReferenceDetectionService
{
    IReadOnlyList<DetectedPerson> DetectPersons(string? text);

    IReadOnlyList<DetectedReference> DetectReferences(string? text);
}
