namespace AtlasDrop.Core.Naming;

public sealed record FileRenameSuggestion(
    string ProposedFileName,
    bool Changed,
    IReadOnlyList<string> Reasons);
