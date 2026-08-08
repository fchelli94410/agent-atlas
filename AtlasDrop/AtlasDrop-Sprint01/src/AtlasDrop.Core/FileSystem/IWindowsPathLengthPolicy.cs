namespace AtlasDrop.Core.FileSystem;

public interface IWindowsPathLengthPolicy
{
    PathLengthCheckResult Check(
        string path,
        int safeLimit = 240);

    PathLengthCheckResult CheckDestination(
        string destinationDirectory,
        string fileName,
        int safeLimit = 240);
}
