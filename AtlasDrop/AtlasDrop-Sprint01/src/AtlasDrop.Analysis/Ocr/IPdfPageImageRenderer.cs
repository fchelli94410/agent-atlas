namespace AtlasDrop.Analysis.Ocr;

public interface IPdfPageImageRenderer
{
    Task<IReadOnlyList<string>> RenderPagesAsync(
        string pdfPath,
        string outputDirectory,
        int maxPages,
        CancellationToken cancellationToken);
}
