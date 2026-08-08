using AtlasDrop.Core.Analysis;

namespace AtlasDrop.Analysis.Ocr;

public sealed class TesseractImageOcrService : IImageOcrService
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".tif",
            ".tiff"
        };

    private readonly string _tessDataPath;
    private readonly string _languages;
    private readonly int _maxCharacters;
    private readonly ITesseractBackend _backend;

    public TesseractImageOcrService(
        string tessDataPath,
        string languages = "fra",
        int maxCharacters = 250_000,
        ITesseractBackend? backend = null)
    {
        if (string.IsNullOrWhiteSpace(tessDataPath))
            throw new ArgumentException("Le chemin tessdata est obligatoire.", nameof(tessDataPath));

        if (string.IsNullOrWhiteSpace(languages))
            throw new ArgumentException("La langue OCR est obligatoire.", nameof(languages));

        if (maxCharacters <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCharacters));

        _tessDataPath = Path.GetFullPath(tessDataPath);
        _languages = languages.Trim();
        _maxCharacters = maxCharacters;
        _backend = backend ?? new TesseractBackend();
    }

    public bool CanHandle(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        return SupportedExtensions.Contains(Path.GetExtension(filePath));
    }

    public Task<OcrResult> RecognizeAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Le chemin image est obligatoire.", nameof(filePath));

        var fullPath = Path.GetFullPath(filePath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException("L'image OCR est introuvable.", fullPath);

        if (!CanHandle(fullPath))
            throw new NotSupportedException(
                $"Extension OCR non prise en charge : {Path.GetExtension(fullPath)}");

        ValidateTessData();

        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = _backend.Recognize(
                    fullPath,
                    _tessDataPath,
                    _languages,
                    cancellationToken);

                var text = result.Text?.Trim() ?? string.Empty;
                var truncated = text.Length > _maxCharacters;

                if (truncated)
                    text = text[.._maxCharacters];

                return new OcrResult(
                    text,
                    Math.Clamp(result.Confidence, 0f, 1f),
                    _languages,
                    truncated);
            },
            cancellationToken);
    }

    private void ValidateTessData()
    {
        if (!Directory.Exists(_tessDataPath))
            throw new DirectoryNotFoundException(
                $"Dossier tessdata introuvable : {_tessDataPath}");

        foreach (var language in _languages.Split('+', StringSplitOptions.RemoveEmptyEntries))
        {
            var trainedData = Path.Combine(
                _tessDataPath,
                $"{language.Trim()}.traineddata");

            if (!File.Exists(trainedData))
                throw new FileNotFoundException(
                    $"Données Tesseract manquantes pour la langue '{language.Trim()}'.",
                    trainedData);
        }
    }
}
