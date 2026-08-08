using System.Text.Json.Serialization;

namespace AtlasDrop.Infrastructure.Updates;

public sealed class UpdateManifest
{
    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; init; }

    [JsonPropertyName("product")]
    public string Product { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("platform")]
    public string Platform { get; init; } = string.Empty;

    [JsonPropertyName("installer")]
    public string Installer { get; init; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; init; }

    [JsonPropertyName("sha256")]
    public string Sha256 { get; init; } = string.Empty;

    [JsonPropertyName("parts")]
    public List<UpdatePart> Parts { get; init; } = [];
}

public sealed class UpdatePart
{
    [JsonPropertyName("file")]
    public string File { get; init; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; init; }

    [JsonPropertyName("sha256")]
    public string Sha256 { get; init; } = string.Empty;
}

public enum UpdatePreparationStatus
{
    NoUpdate,
    Ready,
    Failed
}

public sealed record UpdatePreparationResult(
    UpdatePreparationStatus Status,
    Version? Version = null,
    string? InstallerPath = null);
