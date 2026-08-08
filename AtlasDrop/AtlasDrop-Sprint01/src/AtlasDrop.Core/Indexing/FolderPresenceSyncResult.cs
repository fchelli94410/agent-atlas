namespace AtlasDrop.Core.Indexing;

public sealed record FolderPresenceSyncResult(
    int MarkedActive,
    int MarkedMissing,
    DateTime SynchronizedUtc);
