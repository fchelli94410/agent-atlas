namespace AtlasDrop.Core.Search;

public sealed class NormalizationOptions
{
    public IReadOnlyDictionary<string, string> Aliases { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["stmaurice"] = "saint maurice",
            ["st-maurice"] = "saint maurice",
            ["st maurice"] = "saint maurice",
            ["montevrain"] = "montevrain",
            ["montévrain"] = "montevrain"
        };
}
