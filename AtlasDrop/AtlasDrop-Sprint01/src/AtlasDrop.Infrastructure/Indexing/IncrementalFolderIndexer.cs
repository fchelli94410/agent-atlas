using AtlasDrop.Core.Indexing;
namespace AtlasDrop.Infrastructure.Indexing;
public sealed class IncrementalFolderIndexer : IIncrementalFolderIndexer
{
    public IncrementalScanResult Compare(IReadOnlyCollection<FolderScanItem> current, IReadOnlyCollection<IndexedFolderSnapshot> previous)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(previous);
        var currentByPath = current.ToDictionary(x => Normalize(x.FullPath), x => x, StringComparer.OrdinalIgnoreCase);
        var previousByPath = previous.ToDictionary(x => Normalize(x.FullPath), x => x, StringComparer.OrdinalIgnoreCase);
        var added = new List<FolderScanItem>();
        var changed = new List<FolderScanItem>();
        var unchanged = new List<FolderScanItem>();
        var removed = new List<string>();
        foreach (var pair in currentByPath)
        {
            if (!previousByPath.TryGetValue(pair.Key, out var old)) { added.Add(pair.Value); continue; }
            if (HasChanged(pair.Value, old)) changed.Add(pair.Value); else unchanged.Add(pair.Value);
        }
        foreach (var pair in previousByPath)
            if (!currentByPath.ContainsKey(pair.Key)) removed.Add(pair.Value.FullPath);
        return new IncrementalScanResult(
            added.OrderBy(x=>x.FullPath,StringComparer.OrdinalIgnoreCase).ToArray(),
            changed.OrderBy(x=>x.FullPath,StringComparer.OrdinalIgnoreCase).ToArray(),
            removed.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToArray(),
            unchanged.OrderBy(x=>x.FullPath,StringComparer.OrdinalIgnoreCase).ToArray());
    }
    private static bool HasChanged(FolderScanItem current, IndexedFolderSnapshot previous)
    {
        if (current.ModifiedUtc != previous.ModifiedUtc || current.FileCount != previous.FileCount) return true;
        var a=current.FileExtensions.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToArray();
        var b=previous.FileExtensions.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToArray();
        return !a.SequenceEqual(b,StringComparer.OrdinalIgnoreCase);
    }
    private static string Normalize(string path)=>Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
