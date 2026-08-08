namespace AtlasDrop.Core.FileSystem;

public interface IMovePreflightService
{
    MovePreflightResult Validate(MovePreflightRequest request);
}
