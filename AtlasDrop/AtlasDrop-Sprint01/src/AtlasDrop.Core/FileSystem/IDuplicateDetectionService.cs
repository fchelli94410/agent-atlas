namespace AtlasDrop.Core.FileSystem;

public interface IDuplicateDetectionService
{
    DuplicateCheckResult Check(DuplicateCheckRequest request);
}
