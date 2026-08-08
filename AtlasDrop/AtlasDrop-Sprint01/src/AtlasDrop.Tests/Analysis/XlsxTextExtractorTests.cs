using Xunit;
using AtlasDrop.Analysis.Office;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace AtlasDrop.Tests.Analysis;

public sealed class XlsxTextExtractorTests
{
    [Theory]
    [InlineData("document.xlsx", true)]
    [InlineData("DOCUMENT.XLSX", true)]
    [InlineData("document.xls", false)]
    [InlineData("document.csv", false)]
    public void CanHandle_only_xlsx(string fileName, bool expected)
    {
        var extractor = new XlsxTextExtractor();
        Assert.Equal(expected, extractor.CanHandle(fileName));
    }

    [Fact]
    public async Task Extracts_sheet_name_and_cells()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "test.xlsx");
            CreateWorkbook(
                file,
                "Factures",
                new[]
                {
                    new[] { "Fournisseur", "Ville", "Montant" },
                    new[] { "EDF", "Courbevoie", "125.50" }
                });

            var result = await new XlsxTextExtractor().ExtractAsync(file);

            Assert.Contains("[Feuille: Factures]", result.Text);
            Assert.Contains("Fournisseur", result.Text);
            Assert.Contains("EDF", result.Text);
            Assert.Contains("Courbevoie", result.Text);
            Assert.Contains("125.50", result.Text);
            Assert.Equal("openxml-xlsx", result.DetectedEncoding);
            Assert.False(result.WasTruncated);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Extracts_multiple_sheets()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "multi.xlsx");
            CreateWorkbookWithTwoSheets(file);

            var result = await new XlsxTextExtractor().ExtractAsync(file);

            Assert.Contains("[Feuille: 2025]", result.Text);
            Assert.Contains("[Feuille: 2026]", result.Text);
            Assert.Contains("Ancienne facture", result.Text);
            Assert.Contains("Nouvelle facture", result.Text);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Character_limit_is_respected()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "large.xlsx");
            CreateWorkbook(
                file,
                "Data",
                new[]
                {
                    new[] { new string('X', 1000) }
                });

            var result = await new XlsxTextExtractor(maxCharacters: 100).ExtractAsync(file);

            Assert.True(result.WasTruncated);
            Assert.True(result.Text.Length <= 100);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Cell_limit_is_respected()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "cells.xlsx");
            CreateWorkbook(
                file,
                "Data",
                new[]
                {
                    new[] { "A", "B", "C", "D", "E" }
                });

            var result = await new XlsxTextExtractor(maxCells: 2).ExtractAsync(file);

            Assert.True(result.WasTruncated);
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Missing_xlsx_is_rejected()
    {
        var missing = Path.Combine(
            Path.GetTempPath(),
            "AtlasDrop.Tests",
            Guid.NewGuid().ToString("N"),
            "missing.xlsx");

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => new XlsxTextExtractor().ExtractAsync(missing));
    }

    [Fact]
    public async Task Unsupported_extension_is_rejected()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "fake.txt");
            await File.WriteAllTextAsync(file, "fake");

            await Assert.ThrowsAsync<NotSupportedException>(
                () => new XlsxTextExtractor().ExtractAsync(file));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Cancellation_is_respected()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "cancel.xlsx");
            CreateWorkbook(
                file,
                "Data",
                new[]
                {
                    new[] { "A", "B", "C" }
                });

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => new XlsxTextExtractor().ExtractAsync(file, cts.Token));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    [Fact]
    public async Task Original_xlsx_is_not_modified()
    {
        var root = CreateTempRoot();

        try
        {
            var file = Path.Combine(root, "readonly.xlsx");
            CreateWorkbook(file, "Data", new[] { new[] { "Original" } });

            var beforeLength = new FileInfo(file).Length;
            var beforeWrite = File.GetLastWriteTimeUtc(file);

            _ = await new XlsxTextExtractor().ExtractAsync(file);

            Assert.Equal(beforeLength, new FileInfo(file).Length);
            Assert.Equal(beforeWrite, File.GetLastWriteTimeUtc(file));
        }
        finally
        {
            DeleteTree(root);
        }
    }

    private static void CreateWorkbook(
        string path,
        string sheetName,
        IReadOnlyList<string[]> rows)
    {
        using var document = SpreadsheetDocument.Create(
            path,
            SpreadsheetDocumentType.Workbook);

        var workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();

        var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        var sheetData = new SheetData();
        worksheetPart.Worksheet = new Worksheet(sheetData);

        foreach (var values in rows)
        {
            var row = new Row();

            foreach (var value in values)
            {
                row.Append(new Cell
                {
                    DataType = CellValues.InlineString,
                    InlineString = new InlineString(new Text(value))
                });
            }

            sheetData.Append(row);
        }

        var sheets = workbookPart.Workbook.AppendChild(new Sheets());
        sheets.Append(new Sheet
        {
            Id = workbookPart.GetIdOfPart(worksheetPart),
            SheetId = 1,
            Name = sheetName
        });

        workbookPart.Workbook.Save();
    }

    private static void CreateWorkbookWithTwoSheets(string path)
    {
        using var document = SpreadsheetDocument.Create(
            path,
            SpreadsheetDocumentType.Workbook);

        var workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();
        var sheets = workbookPart.Workbook.AppendChild(new Sheets());

        AddSheet(workbookPart, sheets, 1, "2025", "Ancienne facture");
        AddSheet(workbookPart, sheets, 2, "2026", "Nouvelle facture");

        workbookPart.Workbook.Save();
    }

    private static void AddSheet(
        WorkbookPart workbookPart,
        Sheets sheets,
        uint sheetId,
        string name,
        string value)
    {
        var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        var sheetData = new SheetData();
        var row = new Row();

        row.Append(new Cell
        {
            DataType = CellValues.InlineString,
            InlineString = new InlineString(new Text(value))
        });

        sheetData.Append(row);
        worksheetPart.Worksheet = new Worksheet(sheetData);

        sheets.Append(new Sheet
        {
            Id = workbookPart.GetIdOfPart(worksheetPart),
            SheetId = sheetId,
            Name = name
        });
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "AtlasDrop.Tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteTree(string path)
    {
        if (!Directory.Exists(path))
            return;

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }
}
