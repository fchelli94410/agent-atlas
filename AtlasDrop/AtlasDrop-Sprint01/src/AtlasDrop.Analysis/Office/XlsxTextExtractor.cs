using System.Globalization;
using System.Text;
using AtlasDrop.Core.Analysis;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace AtlasDrop.Analysis.Office;

public sealed class XlsxTextExtractor : IXlsxTextExtractor
{
    private readonly int _maxCharacters;
    private readonly int _maxCells;

    public XlsxTextExtractor(
        int maxCharacters = 500_000,
        int maxCells = 50_000)
    {
        if (maxCharacters <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCharacters));

        if (maxCells <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCells));

        _maxCharacters = maxCharacters;
        _maxCells = maxCells;
    }

    public bool CanHandle(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        return string.Equals(
            Path.GetExtension(filePath),
            ".xlsx",
            StringComparison.OrdinalIgnoreCase);
    }

    public Task<TextExtractionResult> ExtractAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Le chemin XLSX est obligatoire.", nameof(filePath));

        var fullPath = Path.GetFullPath(filePath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Le fichier XLSX est introuvable.", fullPath);

        if (!CanHandle(fullPath))
            throw new NotSupportedException(
                $"Extension non prise en charge : {Path.GetExtension(fullPath)}");

        return Task.Run(
            () => ExtractInternal(fullPath, cancellationToken),
            cancellationToken);
    }

    private TextExtractionResult ExtractInternal(
        string fullPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var document = SpreadsheetDocument.Open(fullPath, false);

        var workbookPart = document.WorkbookPart
                           ?? throw new InvalidDataException("XLSX sans classeur.");

        var workbook = workbookPart.Workbook;
        var sheets = workbook.Sheets?.Elements<Sheet>().ToArray()
                     ?? Array.Empty<Sheet>();

        var builder = new StringBuilder();
        var cellCount = 0;
        var truncated = false;

        foreach (var sheet in sheets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (builder.Length > 0)
                AppendLimited(builder, Environment.NewLine, ref truncated);

            AppendLimited(
                builder,
                $"[Feuille: {sheet.Name?.Value ?? "Sans nom"}]{Environment.NewLine}",
                ref truncated);

            if (truncated)
                break;

            if (sheet.Id?.Value is null)
                continue;

            var worksheetPart =
                (WorksheetPart)workbookPart.GetPartById(sheet.Id.Value);

            var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;

            foreach (var row in worksheetPart.Worksheet.Descendants<Row>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var rowValues = new List<string>();

                foreach (var cell in row.Elements<Cell>())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    cellCount++;
                    if (cellCount > _maxCells)
                    {
                        truncated = true;
                        break;
                    }

                    var value = GetCellText(cell, sharedStrings);
                    if (!string.IsNullOrWhiteSpace(value))
                        rowValues.Add(value.Trim());
                }

                if (rowValues.Count > 0)
                {
                    AppendLimited(
                        builder,
                        string.Join(" | ", rowValues) + Environment.NewLine,
                        ref truncated);
                }

                if (truncated)
                    break;
            }

            if (truncated)
                break;
        }

        return new TextExtractionResult(
            builder.ToString().Trim(),
            "openxml-xlsx",
            truncated,
            new FileInfo(fullPath).Length);
    }

    private static string GetCellText(
        Cell cell,
        SharedStringTable? sharedStrings)
    {
        var raw = cell.CellValue?.InnerText ?? cell.InnerText ?? string.Empty;

        if (cell.DataType?.Value == CellValues.SharedString &&
            int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) &&
            sharedStrings is not null &&
            index >= 0 &&
            index < sharedStrings.ChildElements.Count)
        {
            return sharedStrings.ChildElements[index].InnerText;
        }

        if (cell.DataType?.Value == CellValues.Boolean)
            return raw == "1" ? "TRUE" : "FALSE";

        if (cell.DataType?.Value == CellValues.InlineString)
            return cell.InlineString?.InnerText ?? raw;

        return raw;
    }

    private void AppendLimited(
        StringBuilder builder,
        string value,
        ref bool truncated)
    {
        if (truncated || string.IsNullOrEmpty(value))
            return;

        var remaining = _maxCharacters - builder.Length;

        if (remaining <= 0)
        {
            truncated = true;
            return;
        }

        if (value.Length <= remaining)
        {
            builder.Append(value);
            return;
        }

        builder.Append(value.AsSpan(0, remaining));
        truncated = true;
    }
}
