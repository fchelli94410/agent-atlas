namespace AtlasDrop.Core.FileSystem;

public interface IFileHashService
{
    string ComputeSha256(string filePath);
}
