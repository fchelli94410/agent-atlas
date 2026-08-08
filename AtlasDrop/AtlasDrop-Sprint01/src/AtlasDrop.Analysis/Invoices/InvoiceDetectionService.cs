using System.Globalization;
using System.Text.RegularExpressions;
using AtlasDrop.Core.Analysis;

namespace AtlasDrop.Analysis.Invoices;

public sealed class InvoiceDetectionService : IInvoiceDetectionService
{
    private static readonly Regex InvoiceNumberPattern = new(
        @"(?i)\b(?:facture|invoice)\s*(?:n[°o]\s*)?[:#\-]?\s*(?<num>[A-Z0-9][A-Z0-9._/\-]{2,40})\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex AmountPattern = new(
        @"(?<!\d)(?<amount>\d{1,3}(?:[ \u00A0]\d{3})*(?:[.,]\d{1,2})?|\d+(?:[.,]\d{1,2})?)\s*(?<currency>€|EUR|USD|\$|GBP|£)(?!\p{L})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex CurrencyBeforePattern = new(
        @"(?<!\p{L})(?<currency>€|\$|£)\s*(?<amount>\d{1,3}(?:[ \u00A0]\d{3})*(?:[.,]\d{1,2})?|\d+(?:[.,]\d{1,2})?)(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] StrongSignals =
    {
        "facture",
        "invoice",
        "total ttc",
        "montant ttc",
        "net à payer",
        "net a payer"
    };

    private static readonly string[] MediumSignals =
    {
        "total ht",
        "tva",
        "échéance",
        "echeance",
        "règlement",
        "reglement",
        "iban"
    };

    public InvoiceDetectionResult Detect(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new InvoiceDetectionResult(
                false,
                0d,
                null,
                Array.Empty<DetectedAmount>(),
                Array.Empty<string>());
        }

        var lowered = text.ToLowerInvariant();
        var signals = new List<string>();

        foreach (var signal in StrongSignals)
        {
            if (lowered.Contains(signal, StringComparison.Ordinal))
                signals.Add(signal);
        }

        foreach (var signal in MediumSignals)
        {
            if (lowered.Contains(signal, StringComparison.Ordinal))
                signals.Add(signal);
        }

        string? invoiceNumber = null;
        var invoiceMatch = InvoiceNumberPattern.Match(text);

        if (invoiceMatch.Success)
        {
            invoiceNumber = NormalizeReference(invoiceMatch.Groups["num"].Value);
            signals.Add("invoice-number");
        }

        var amounts = new List<DetectedAmount>();

        foreach (Match match in AmountPattern.Matches(text))
        {
            if (TryParseAmount(
                match.Groups["amount"].Value,
                out var amount))
            {
                amounts.Add(new DetectedAmount(
                    amount,
                    NormalizeCurrency(match.Groups["currency"].Value),
                    0.92,
                    "amount-after",
                    match.Value));
            }
        }

        foreach (Match match in CurrencyBeforePattern.Matches(text))
        {
            if (TryParseAmount(
                match.Groups["amount"].Value,
                out var amount))
            {
                amounts.Add(new DetectedAmount(
                    amount,
                    NormalizeCurrency(match.Groups["currency"].Value),
                    0.90,
                    "amount-before",
                    match.Value));
            }
        }

        amounts = amounts
            .GroupBy(
                x => $"{x.Value.ToString(CultureInfo.InvariantCulture)}|{x.Currency}",
                StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.Confidence).First())
            .OrderByDescending(x => x.Value)
            .ToList();

        var score = 0d;

        if (signals.Any(x => StrongSignals.Contains(x)))
            score += 0.55;

        if (signals.Any(x => MediumSignals.Contains(x)))
            score += 0.15;

        if (!string.IsNullOrWhiteSpace(invoiceNumber))
            score += 0.20;

        if (amounts.Count > 0)
            score += 0.20;

        score = Math.Clamp(score, 0d, 1d);

        return new InvoiceDetectionResult(
            score >= 0.60,
            score,
            invoiceNumber,
            amounts,
            signals
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private static bool TryParseAmount(
        string raw,
        out decimal amount)
    {
        var normalized = raw
            .Replace("\u00A0", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace(',', '.');

        return decimal.TryParse(
            normalized,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out amount);
    }

    private static string NormalizeCurrency(string raw)
    {
        return raw.Trim().ToUpperInvariant() switch
        {
            "€" or "EUR" => "EUR",
            "$" or "USD" => "USD",
            "£" or "GBP" => "GBP",
            _ => raw.Trim().ToUpperInvariant()
        };
    }

    private static string NormalizeReference(string value)
    {
        var upper = value.Trim().ToUpperInvariant();
        return Regex.Replace(upper, @"[\s._/\\\-]+", "-").Trim('-');
    }
}
