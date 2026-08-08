using Xunit;
using AtlasDrop.Core.Analysis;
using AtlasDrop.Core.Indexing;
using AtlasDrop.Indexing.FileProfiles;

namespace AtlasDrop.Tests.Indexing;

public sealed class FolderFileProfileBuilderTests
{
    [Fact]
    public void Counts_files_and_document_types()
    {
        var files = new[]
        {
            File("a.pdf", DocumentType.Invoice),
            File("b.pdf", DocumentType.Invoice),
            File("c.docx", DocumentType.Contract)
        };

        var result = new FolderFileProfileBuilder().Build(files);

        Assert.Equal(3, result.FileCount);
        Assert.Equal(2, result.DocumentTypeCounts[DocumentType.Invoice]);
        Assert.Equal(1, result.DocumentTypeCounts[DocumentType.Contract]);
    }

    [Fact]
    public void Counts_extensions_case_insensitively()
    {
        var files = new[]
        {
            File("a.PDF", DocumentType.Invoice),
            File("b.pdf", DocumentType.Invoice),
            File("c.docx", DocumentType.Contract)
        };

        var result = new FolderFileProfileBuilder().Build(files);

        Assert.Equal(2, result.ExtensionCounts[".pdf"]);
        Assert.Equal(1, result.ExtensionCounts[".docx"]);
    }

    [Fact]
    public void Keeps_only_useful_frequent_keywords()
    {
        var files = new[]
        {
            new IndexedFileSignal(
                "a.pdf",
                ".pdf",
                DocumentType.Invoice,
                "facture edf courbevoie 2026",
                new[] { "EDF", "Courbevoie" }),
            new IndexedFileSignal(
                "b.pdf",
                ".pdf",
                DocumentType.Invoice,
                "facture edf paris",
                new[] { "EDF" })
        };

        var result = new FolderFileProfileBuilder().Build(files);

        Assert.Contains("edf", result.FrequentKeywords);
        Assert.Contains("facture", result.FrequentKeywords);
        Assert.DoesNotContain("2026", result.FrequentKeywords);
    }

    [Fact]
    public void Same_keyword_counts_once_per_file()
    {
        var files = new[]
        {
            new IndexedFileSignal(
                "a.pdf",
                ".pdf",
                DocumentType.Invoice,
                "edf edf edf",
                new[] { "EDF", "edf" }),
            new IndexedFileSignal(
                "b.pdf",
                ".pdf",
                DocumentType.Invoice,
                "edf",
                Array.Empty<string>())
        };

        var result = new FolderFileProfileBuilder().Build(files);

        Assert.Equal("edf", result.FrequentKeywords[0]);
    }

    [Fact]
    public void Keyword_limit_is_respected()
    {
        var files = new[]
        {
            new IndexedFileSignal(
                "a.txt",
                ".txt",
                DocumentType.Report,
                "alpha bravo charlie delta echo",
                Array.Empty<string>())
        };

        var result = new FolderFileProfileBuilder().Build(
            files,
            maxKeywords: 3);

        Assert.Equal(3, result.FrequentKeywords.Count);
    }

    [Fact]
    public void Unknown_document_type_is_not_counted()
    {
        var files = new[]
        {
            File("a.bin", DocumentType.Unknown)
        };

        var result = new FolderFileProfileBuilder().Build(files);

        Assert.Empty(result.DocumentTypeCounts);
        Assert.Equal(1, result.FileCount);
    }

    [Fact]
    public void Empty_collection_returns_empty_profile()
    {
        var result = new FolderFileProfileBuilder().Build(
            Array.Empty<IndexedFileSignal>());

        Assert.Equal(0, result.FileCount);
        Assert.Empty(result.DocumentTypeCounts);
        Assert.Empty(result.ExtensionCounts);
        Assert.Empty(result.FrequentKeywords);
    }

    [Fact]
    public void Invalid_keyword_limit_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new FolderFileProfileBuilder().Build(
                Array.Empty<IndexedFileSignal>(),
                maxKeywords: 0));
    }

    private static IndexedFileSignal File(
        string name,
        DocumentType type)
    {
        return new IndexedFileSignal(
            name,
            Path.GetExtension(name),
            type,
            Path.GetFileNameWithoutExtension(name).ToLowerInvariant(),
            Array.Empty<string>());
    }
}
