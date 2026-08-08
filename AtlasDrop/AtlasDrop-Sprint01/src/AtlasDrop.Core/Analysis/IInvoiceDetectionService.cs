namespace AtlasDrop.Core.Analysis;

public interface IInvoiceDetectionService
{
    InvoiceDetectionResult Detect(string? text);
}
