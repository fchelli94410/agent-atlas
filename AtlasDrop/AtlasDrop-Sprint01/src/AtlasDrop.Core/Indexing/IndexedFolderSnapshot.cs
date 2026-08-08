namespace AtlasDrop.Core.Indexing;
public sealed record IndexedFolderSnapshot(string FullPath, DateTime ModifiedUtc, int FileCount, IReadOnlyCollection<string> FileExtensions);
