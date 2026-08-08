using AtlasDrop.Core.Analysis;

namespace AtlasDrop.Core.Naming;

public sealed record FileRenameContext(
    string OriginalFileName,
    DocumentType DocumentType,
    DateTime? ExactDate,
    int? Year,
    int? Month,
    string? Place,
    string? Company,
    string? Detail,
    string? Reference);
