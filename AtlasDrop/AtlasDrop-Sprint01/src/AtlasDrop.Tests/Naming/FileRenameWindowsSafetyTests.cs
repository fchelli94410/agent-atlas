using Xunit;
using AtlasDrop.Analysis.Naming;
using AtlasDrop.Core.Analysis;
using AtlasDrop.Core.Naming;

namespace AtlasDrop.Tests.Naming;

public sealed class FileRenameWindowsSafetyTests
{
    [Fact]
    public void Proposed_rename_is_always_windows_valid()
    {
        var result = new FileRenameSuggestionService().Suggest(
            new FileRenameContext(
                "scan.pdf",
                DocumentType.Invoice,
                null,
                2026,
                null,
                "Paris/France",
                "CON",
                "Client: EDF ?",
                null));

        Assert.DoesNotContain("?", result.ProposedFileName);
        Assert.DoesNotContain("/", result.ProposedFileName);
        Assert.DoesNotContain("|", result.ProposedFileName);
    }

    [Fact]
    public void Long_detail_is_safely_limited()
    {
        var result = new FileRenameSuggestionService().Suggest(
            new FileRenameContext(
                "scan.pdf",
                DocumentType.Report,
                null,
                2026,
                null,
                null,
                null,
                new string('X', 400),
                null));

        Assert.True(result.ProposedFileName.Length <= 180);
    }
}
