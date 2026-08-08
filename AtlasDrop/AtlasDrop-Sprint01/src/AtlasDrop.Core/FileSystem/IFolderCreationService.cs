namespace AtlasDrop.Core.FileSystem;

public interface IFolderCreationService
{
    FolderCreationResult Validate(FolderCreationRequest request);

    Task<FolderCreationResult> CreateAsync(
        FolderCreationRequest request,
        CancellationToken cancellationToken = default);
}
