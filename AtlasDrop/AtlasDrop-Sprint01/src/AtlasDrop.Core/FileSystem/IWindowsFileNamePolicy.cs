namespace AtlasDrop.Core.FileSystem;

public interface IWindowsFileNamePolicy
{
    SafeFileNameResult Sanitize(
        string fileName,
        int maxFileNameLength = 180);

    bool IsValid(string fileName);
}
