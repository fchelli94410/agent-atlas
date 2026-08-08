namespace AtlasDrop.Core.Configuration;

public sealed class AtlasDropOptions
{
    public string OneDriveRoot { get; init; } =
        @"C:\Users\fchelli\OneDrive - ALTEDIS";

    public int MaxSuggestedDepth { get; init; } = 5;

    public IReadOnlyCollection<string> ExcludedFolderNames { get; init; } =
        new[]
        {
            "Bureau",
            "Documents",
            "Images",
            "99 - Archives",
            "pour voir"
        };
}
