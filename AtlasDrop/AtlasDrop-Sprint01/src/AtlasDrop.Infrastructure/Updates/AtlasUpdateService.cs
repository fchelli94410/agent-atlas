using System.Buffers;
using System.Security.Cryptography;
using System.Text.Json;
using AtlasDrop.Core.Logging;

namespace AtlasDrop.Infrastructure.Updates;

public sealed class AtlasUpdateService
{
    public static readonly Uri DefaultManifestUri =
        new("https://atlas-drop-updates.chellifranck.chatgpt.site/manifest.json");

    private const long MaxInstallerSize = 500L * 1024L * 1024L;
    private const int MaxParts = 128;

    private readonly HttpClient _httpClient;
    private readonly IAtlasLogger _logger;
    private readonly Uri _manifestUri;
    private readonly string _updateDirectory;

    public AtlasUpdateService(
        HttpClient httpClient,
        IAtlasLogger logger,
        Uri? manifestUri = null,
        string? updateDirectory = null)
    {
        _httpClient = httpClient
            ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger
            ?? throw new ArgumentNullException(nameof(logger));
        _manifestUri = manifestUri ?? DefaultManifestUri;

        if (_manifestUri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException(
                "Le manifeste doit utiliser HTTPS.",
                nameof(manifestUri));

        _updateDirectory = updateDirectory
            ?? Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "AtlasDrop",
                "Updates");
    }

    public async Task<UpdatePreparationResult> PrepareUpdateAsync(
        Version currentVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currentVersion);

        string? stagingPath = null;

        try
        {
            using var manifestResponse =
                await _httpClient.GetAsync(
                    _manifestUri,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);

            EnsureTrustedResponse(manifestResponse);
            manifestResponse.EnsureSuccessStatusCode();

            await using var manifestStream =
                await manifestResponse.Content.ReadAsStreamAsync(
                    cancellationToken);

            var manifest =
                await JsonSerializer.DeserializeAsync<UpdateManifest>(
                    manifestStream,
                    cancellationToken: cancellationToken)
                ?? throw new InvalidDataException(
                    "Le manifeste de mise à jour est vide.");

            var remoteVersion = ValidateManifest(manifest);

            if (remoteVersion <= currentVersion)
            {
                return new UpdatePreparationResult(
                    UpdatePreparationStatus.NoUpdate,
                    remoteVersion);
            }

            Directory.CreateDirectory(_updateDirectory);

            stagingPath = Path.Combine(
                _updateDirectory,
                $"AtlasDrop-{remoteVersion}-{Guid.NewGuid():N}.download");

            await using var output = new FileStream(
                stagingPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                128 * 1024,
                FileOptions.Asynchronous |
                FileOptions.SequentialScan);

            using var fullHash =
                IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

            long totalBytes = 0;

            foreach (var part in manifest.Parts)
            {
                var partUri = ResolvePartUri(part.File);

                using var partResponse =
                    await _httpClient.GetAsync(
                        partUri,
                        HttpCompletionOption.ResponseHeadersRead,
                        cancellationToken);

                EnsureTrustedResponse(partResponse);
                partResponse.EnsureSuccessStatusCode();

                if (partResponse.Content.Headers.ContentLength is long length &&
                    length != part.Size)
                {
                    throw new InvalidDataException(
                        "La taille d'un morceau est incorrecte.");
                }

                await using var partStream =
                    await partResponse.Content.ReadAsStreamAsync(
                        cancellationToken);

                using var partHash =
                    IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

                var buffer = ArrayPool<byte>.Shared.Rent(128 * 1024);
                long partBytes = 0;

                try
                {
                    while (true)
                    {
                        var read = await partStream.ReadAsync(
                            buffer.AsMemory(0, buffer.Length),
                            cancellationToken);

                        if (read == 0)
                            break;

                        partBytes += read;
                        totalBytes += read;

                        if (partBytes > part.Size ||
                            totalBytes > MaxInstallerSize)
                        {
                            throw new InvalidDataException(
                                "La mise à jour dépasse la taille autorisée.");
                        }

                        partHash.AppendData(buffer, 0, read);
                        fullHash.AppendData(buffer, 0, read);

                        await output.WriteAsync(
                            buffer.AsMemory(0, read),
                            cancellationToken);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                if (partBytes != part.Size ||
                    !HashMatches(
                        part.Sha256,
                        partHash.GetHashAndReset()))
                {
                    throw new InvalidDataException(
                        "Un morceau de mise à jour est corrompu.");
                }
            }

            await output.FlushAsync(cancellationToken);

            if (totalBytes != manifest.Size ||
                !HashMatches(
                    manifest.Sha256,
                    fullHash.GetHashAndReset()))
            {
                throw new InvalidDataException(
                    "L'installateur reconstitué est corrompu.");
            }

            await output.DisposeAsync();

            var installerPath = Path.Combine(
                _updateDirectory,
                $"Installer-Atlas-Drop-{remoteVersion}.exe");

            File.Move(
                stagingPath,
                installerPath,
                true);

            stagingPath = null;

            _logger.Information(
                "UpdateReady",
                $"Mise à jour {remoteVersion} vérifiée et prête.");

            return new UpdatePreparationResult(
                UpdatePreparationStatus.Ready,
                remoteVersion,
                installerPath);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            DeleteIfPresent(stagingPath);
            throw;
        }
        catch (Exception ex)
        {
            DeleteIfPresent(stagingPath);

            _logger.Error(
                "UpdatePreparationFailed",
                "La vérification de mise à jour a échoué. " +
                "La version installée reste active.",
                ex);

            return new UpdatePreparationResult(
                UpdatePreparationStatus.Failed);
        }
    }

    private Version ValidateManifest(
        UpdateManifest manifest)
    {
        if (manifest.SchemaVersion != 1 ||
            !string.Equals(
                manifest.Product,
                "Atlas Drop",
                StringComparison.Ordinal) ||
            !string.Equals(
                manifest.Platform,
                "windows-x64",
                StringComparison.Ordinal) ||
            !Version.TryParse(
                manifest.Version,
                out var remoteVersion) ||
            string.IsNullOrWhiteSpace(manifest.Installer) ||
            Path.GetFileName(manifest.Installer) != manifest.Installer ||
            manifest.Size <= 0 ||
            manifest.Size > MaxInstallerSize ||
            manifest.Parts.Count is < 1 or > MaxParts)
        {
            throw new InvalidDataException(
                "Le manifeste de mise à jour est invalide.");
        }

        long declaredTotal = 0;

        foreach (var part in manifest.Parts)
        {
            if (part.Size <= 0 ||
                part.Size > MaxInstallerSize ||
                !IsValidSha256(part.Sha256))
            {
                throw new InvalidDataException(
                    "Un morceau du manifeste est invalide.");
            }

            declaredTotal = checked(declaredTotal + part.Size);
            _ = ResolvePartUri(part.File);
        }

        if (declaredTotal != manifest.Size ||
            !IsValidSha256(manifest.Sha256))
        {
            throw new InvalidDataException(
                "Les contrôles du manifeste sont incohérents.");
        }

        return remoteVersion;
    }

    private Uri ResolvePartUri(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) ||
            Uri.TryCreate(relativePath, UriKind.Absolute, out _) ||
            relativePath.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Chemin de morceau non autorisé.");
        }

        var resolved = new Uri(_manifestUri, relativePath);

        if (resolved.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(
                resolved.Host,
                _manifestUri.Host,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Hôte de téléchargement non autorisé.");
        }

        return resolved;
    }

    private void EnsureTrustedResponse(
        HttpResponseMessage response)
    {
        var finalUri = response.RequestMessage?.RequestUri;

        if (finalUri is null ||
            finalUri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(
                finalUri.Host,
                _manifestUri.Host,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Redirection de téléchargement non autorisée.");
        }
    }

    private static bool HashMatches(
        string expectedHex,
        byte[] actualHash)
    {
        try
        {
            var expectedHash =
                Convert.FromHexString(expectedHex);

            return expectedHash.Length == actualHash.Length &&
                CryptographicOperations.FixedTimeEquals(
                    expectedHash,
                    actualHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsValidSha256(
        string value)
    {
        if (value.Length != 64)
            return false;

        try
        {
            return Convert.FromHexString(value).Length == 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static void DeleteIfPresent(
        string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }
}
