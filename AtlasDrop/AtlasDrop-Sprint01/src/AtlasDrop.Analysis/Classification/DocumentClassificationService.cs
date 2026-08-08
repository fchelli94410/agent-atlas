using AtlasDrop.Core.Analysis;

namespace AtlasDrop.Analysis.Classification;

public sealed class DocumentClassificationService
    : IDocumentClassificationService
{
    private readonly IInvoiceDetectionService _invoiceDetection;

    public DocumentClassificationService(
        IInvoiceDetectionService invoiceDetection)
    {
        _invoiceDetection = invoiceDetection
            ?? throw new ArgumentNullException(nameof(invoiceDetection));
    }

    public DocumentClassificationResult Classify(
        string fileName,
        string? extractedText = null)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException(
                "Le nom de fichier est obligatoire.",
                nameof(fileName));

        var reasons = new List<string>();
        var scores = new Dictionary<DocumentType, double>();

        void AddScore(DocumentType type, double score, string reason)
        {
            if (!scores.TryAdd(type, score))
                scores[type] += score;

            reasons.Add($"{type}: {reason}");
        }

        var extension = Path.GetExtension(fileName)
            .ToLowerInvariant();

        ApplyExtensionSignals(extension, AddScore);

        var text = string.IsNullOrWhiteSpace(extractedText)
            ? string.Empty
            : extractedText.Trim();

        if (text.Length > 0)
        {
            var lowered = text.ToLowerInvariant();

            var invoice = _invoiceDetection.Detect(text);
            if (invoice.IsLikelyInvoice)
            {
                AddScore(
                    DocumentType.Invoice,
                    0.70,
                    $"signaux facture ({invoice.Confidence:0.00})");
            }

            AddKeywordSignals(lowered, AddScore);
        }

        if (scores.Count == 0)
        {
            return new DocumentClassificationResult(
                DocumentType.Unknown,
                0.15,
                new[] { "Aucun signal suffisamment discriminant." });
        }

        var ranked = scores
            .Select(x => new
            {
                Type = x.Key,
                Score = Math.Clamp(x.Value, 0d, 1d)
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Type)
            .ToArray();

        var best = ranked[0];
        var second = ranked.Length > 1 ? ranked[1].Score : 0d;

        var separationBonus = Math.Clamp(
            best.Score - second,
            0d,
            0.20);

        var confidence = Math.Clamp(
            best.Score + separationBonus,
            0d,
            1d);

        var selectedReasons = reasons
            .Where(x => x.StartsWith(
                best.Type + ":",
                StringComparison.Ordinal))
            .Select(x => x[(x.IndexOf(':') + 1)..].Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new DocumentClassificationResult(
            best.Type,
            confidence,
            selectedReasons);
    }

    private static void ApplyExtensionSignals(
        string extension,
        Action<DocumentType, double, string> add)
    {
        switch (extension)
        {
            case ".xlsx":
            case ".xls":
            case ".csv":
                add(
                    DocumentType.Spreadsheet,
                    0.65,
                    $"extension {extension}");
                break;

            case ".pptx":
            case ".ppt":
                add(
                    DocumentType.Presentation,
                    0.70,
                    $"extension {extension}");
                break;

            case ".msg":
            case ".eml":
                add(
                    DocumentType.Email,
                    0.75,
                    $"extension {extension}");
                break;

            case ".jpg":
            case ".jpeg":
            case ".png":
            case ".tif":
            case ".tiff":
                add(
                    DocumentType.Image,
                    0.60,
                    $"extension {extension}");
                break;

            case ".zip":
                add(
                    DocumentType.Archive,
                    0.85,
                    "extension .zip");
                break;
        }
    }

    private static void AddKeywordSignals(
        string text,
        Action<DocumentType, double, string> add)
    {
        if (ContainsAny(text, "devis", "quotation", "quote"))
        {
            add(
                DocumentType.Quote,
                0.70,
                "mot-clé devis/quote");
        }

        if (ContainsAny(text, "contrat", "contract", "convention"))
        {
            add(
                DocumentType.Contract,
                0.70,
                "mot-clé contrat/convention");
        }

        if (ContainsAny(
            text,
            "bon de commande",
            "purchase order",
            "commande n°",
            "commande no"))
        {
            add(
                DocumentType.Order,
                0.75,
                "mot-clé commande");
        }

        if (ContainsAny(
            text,
            "rapport",
            "report",
            "compte rendu",
            "synthèse",
            "synthese"))
        {
            add(
                DocumentType.Report,
                0.60,
                "mot-clé rapport/compte rendu");
        }

        if (ContainsAny(
            text,
            "relevé",
            "releve",
            "statement",
            "relevé de compte",
            "releve de compte"))
        {
            add(
                DocumentType.Statement,
                0.70,
                "mot-clé relevé");
        }

        if (ContainsAny(
            text,
            "ticket de caisse",
            "reçu",
            "recu",
            "receipt"))
        {
            add(
                DocumentType.Receipt,
                0.70,
                "mot-clé reçu/ticket");
        }

        if (ContainsAny(
            text,
            "madame, monsieur",
            "objet :",
            "cordialement",
            "veuillez agréer",
            "veuillez agreer"))
        {
            add(
                DocumentType.Letter,
                0.45,
                "structure de courrier");
        }
    }

    private static bool ContainsAny(
        string text,
        params string[] values)
    {
        return values.Any(x =>
            text.Contains(
                x,
                StringComparison.OrdinalIgnoreCase));
    }
}
