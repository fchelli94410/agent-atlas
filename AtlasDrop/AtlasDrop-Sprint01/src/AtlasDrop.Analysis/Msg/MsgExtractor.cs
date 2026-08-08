using System.Net;
using System.Text.RegularExpressions;
using AtlasDrop.Core.Analysis;

namespace AtlasDrop.Analysis.Msg;

public sealed class MsgExtractor : IMsgExtractor
{
    private readonly IMsgReaderBackend _backend;
    private readonly int _maxBodyCharacters;

    public MsgExtractor(
        IMsgReaderBackend? backend = null,
        int maxBodyCharacters = 300_000)
    {
        if (maxBodyCharacters <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxBodyCharacters));

        _backend = backend ?? new MsgReaderBackend();
        _maxBodyCharacters = maxBodyCharacters;
    }

    public bool CanHandle(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        return string.Equals(
            Path.GetExtension(filePath),
            ".msg",
            StringComparison.OrdinalIgnoreCase);
    }

    public Task<MsgAnalysisResult> ExtractAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Le chemin MSG est obligatoire.", nameof(filePath));

        var fullPath = Path.GetFullPath(filePath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Le fichier MSG est introuvable.", fullPath);

        if (!CanHandle(fullPath))
            throw new NotSupportedException(
                $"Extension non prise en charge : {Path.GetExtension(fullPath)}");

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var raw = _backend.Read(fullPath, cancellationToken);

                var body = !string.IsNullOrWhiteSpace(raw.BodyText)
                    ? raw.BodyText!
                    : HtmlToPlainText(raw.BodyHtml);

                body = body.Trim();

                var truncated = body.Length > _maxBodyCharacters;
                if (truncated)
                    body = body[.._maxBodyCharacters];

                return new MsgAnalysisResult(
                    Clean(raw.Subject),
                    Clean(raw.Sender),
                    Clean(raw.RecipientsTo),
                    Clean(raw.RecipientsCc),
                    raw.SentOn,
                    body,
                    truncated);
            },
            cancellationToken);
    }

    private static string Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string HtmlToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var withLines = Regex.Replace(
            html,
            @"<(br|/p|/div|/li)\b[^>]*>",
            Environment.NewLine,
            RegexOptions.IgnoreCase);

        var withoutTags = Regex.Replace(
            withLines,
            @"<[^>]+>",
            " ");

        var decoded = WebUtility.HtmlDecode(withoutTags);

        return Regex.Replace(
            decoded,
            @"[ \t]+",
            " ").Trim();
    }
}
