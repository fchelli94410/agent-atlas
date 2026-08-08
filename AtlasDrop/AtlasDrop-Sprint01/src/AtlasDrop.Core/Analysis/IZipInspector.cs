namespace AtlasDrop.Core.Analysis;

public interface IZipInspector
{
    bool CanHandle(string filePath);

    Task<ZipInspectionResult> InspectAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
