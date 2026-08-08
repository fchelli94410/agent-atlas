using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using AtlasDrop.Core.Integration;
using AtlasDrop.Core.Logging;
using AtlasDrop.Infrastructure.Integration;
using AtlasDrop.Infrastructure.Logging;
using AtlasDrop.Infrastructure.Updates;

namespace AtlasDrop.App;

public partial class App : Application
{
    private IFileActivationChannel? _activationChannel;
    private CancellationTokenSource? _listenerCts;
    private SerilogAtlasLogger? _logger;
    private ExplorerMiddleClickActivation? _middleClickActivation;
    private HttpClient? _updateHttpClient;
    private int _updateLaunchStarted;

    protected override async void OnStartup(
        StartupEventArgs e)
    {
        base.OnStartup(e);

        _logger = new SerilogAtlasLogger(
            AtlasLogPath.GetDefaultDirectory());

        _logger.Information(
            "AppStartup",
            "Atlas Drop démarre.");

        _activationChannel =
            new NamedPipeFileActivationChannel();

        var requestedFile = e.Args.FirstOrDefault();

        if (!_activationChannel.IsPrimaryInstance)
        {
            if (!string.IsNullOrWhiteSpace(requestedFile))
            {
                try
                {
                    await _activationChannel.SendToPrimaryAsync(
                        requestedFile);
                }
                catch
                {
                }
            }

            _logger.Information(
                "SecondaryInstance",
                "Fichier transmis à l'instance principale.");

            Shutdown();
            return;
        }

        var mainWindow = new MainWindow();

        _middleClickActivation =
            new ExplorerMiddleClickActivation();

        _middleClickActivation.ExplorerClickDetected += (_, _) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                mainWindow.SignalMiddleClickDetected();
                mainWindow.Show();
                mainWindow.Activate();
            });
        };

        _middleClickActivation.ItemActivated += (_, itemPath) =>
        {
            _logger?.Information(
                "MiddleClickItemResolved",
                $"Élément détecté : {itemPath}");
            Dispatcher.BeginInvoke(() =>
            {
                mainWindow.ActivateFile(itemPath);

                if (mainWindow.WindowState == WindowState.Minimized)
                    mainWindow.WindowState = WindowState.Normal;

                mainWindow.Show();
                mainWindow.Activate();
            });
        };

        _middleClickActivation.ItemResolutionFailed += (_, diagnostic) =>
        {
            _logger?.Information(
                "MiddleClickItemResolutionFailed",
                $"Clic détecté, mais élément non résolu. {diagnostic}");
        };

        try
        {
            _middleClickActivation.Start();
            _logger.Information(
                "MiddleClickActivationStarted",
                "Le clic molette Explorateur est actif.");
        }
        catch (Exception ex)
        {
            _logger.Error(
                "MiddleClickActivationFailed",
                "Le clic molette n'a pas pu être activé.",
                ex);
        }

        _activationChannel.FileReceived += (_, filePath) =>
        {
            Dispatcher.Invoke(() =>
            {
                mainWindow.ActivateFile(filePath);

                if (mainWindow.WindowState ==
                    WindowState.Minimized)
                {
                    mainWindow.WindowState =
                        WindowState.Normal;
                }

                mainWindow.Activate();
                mainWindow.Topmost = true;
                mainWindow.Topmost = false;
                mainWindow.Focus();
            });
        };

        _listenerCts = new CancellationTokenSource();

        _ = ListenAsync(
            _activationChannel,
            _listenerCts.Token);

        _ = CheckForUpdatesAsync(
            _listenerCts.Token);

        // Atlas Drop stays quietly in the background until an item is
        // activated with the middle mouse button (or through the legacy pipe).
        if (!string.IsNullOrWhiteSpace(requestedFile))
        {
            mainWindow.Show();
            mainWindow.ActivateFile(requestedFile);
        }
    }

    protected override void OnExit(
        ExitEventArgs e)
    {
        _listenerCts?.Cancel();
        _listenerCts?.Dispose();
        _activationChannel?.Dispose();
        _middleClickActivation?.Dispose();
        _updateHttpClient?.Dispose();

        _logger?.Information(
            "AppExit",
            "Atlas Drop se ferme.");

        _logger?.Dispose();

        base.OnExit(e);
    }

    private async Task CheckForUpdatesAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            _updateHttpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(5)
            };

            var updater = new AtlasUpdateService(
                _updateHttpClient,
                _logger!);

            var currentVersion =
                typeof(App).Assembly.GetName().Version
                ?? new Version(1, 0, 0);

            var result = await updater.PrepareUpdateAsync(
                currentVersion,
                cancellationToken);

            if (result.Status != UpdatePreparationStatus.Ready ||
                string.IsNullOrWhiteSpace(result.InstallerPath))
            {
                return;
            }

            await Dispatcher.InvokeAsync(() =>
            {
                if (Interlocked.Exchange(
                    ref _updateLaunchStarted,
                    1) != 0)
                {
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = result.InstallerPath,
                    Arguments =
                        "/VERYSILENT /SUPPRESSMSGBOXES " +
                        "/NORESTART /CLOSEAPPLICATIONS /UPDATE=1",
                    WorkingDirectory =
                        Path.GetDirectoryName(result.InstallerPath),
                    UseShellExecute = true
                });

                _logger?.Information(
                    "UpdateInstallerStarted",
                    $"Installation de la version {result.Version} lancée.");

                Shutdown();
            });
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger?.Error(
                "UpdateLaunchFailed",
                "La mise à jour automatique n'a pas pu démarrer. " +
                "La version installée reste active.",
                ex);
        }
    }

    private static async Task ListenAsync(
        IFileActivationChannel channel,
        CancellationToken cancellationToken)
    {
        try
        {
            await channel.StartListeningAsync(
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
