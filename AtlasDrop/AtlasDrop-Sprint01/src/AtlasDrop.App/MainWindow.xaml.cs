using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using AtlasDrop.Analysis.Classification;
using AtlasDrop.Analysis.Companies;
using AtlasDrop.Analysis.Dates;
using AtlasDrop.Analysis.Invoices;
using AtlasDrop.Analysis.Naming;
using AtlasDrop.Analysis.Office;
using AtlasDrop.Analysis.Pdf;
using AtlasDrop.Analysis.Places;
using AtlasDrop.Analysis.Text;
using AtlasDrop.Core.Analysis;
using AtlasDrop.Core.Configuration;
using AtlasDrop.Core.FileSystem;
using AtlasDrop.Core.Learning;
using AtlasDrop.Core.Naming;
using AtlasDrop.Core.Notifications;
using AtlasDrop.Core.Suggestions;
using AtlasDrop.Infrastructure.FileSystem;
using AtlasDrop.Infrastructure.Learning;
using AtlasDrop.Infrastructure.Notifications;
using AtlasDrop.FileOperations;
using AtlasDrop.Search.Normalization;
using AtlasDrop.Search.Suggestions;

namespace AtlasDrop.App;

public partial class MainWindow : Window
{
    private const int MaxDepth = 4;
    private static readonly HashSet<string> AllowedRootFolderNames = new(
        new[]
        {
            "01 - Immobilier",
            "02 - Activités Professionnelles",
            "03 - Finances personnelles",
            "04 - Quotidien",
            "05 - Projets"
        },
        StringComparer.OrdinalIgnoreCase);
    private const int WeakPositiveLearningWeight = 1;
    private const int StrongPositiveLearningWeight = 3;
    private const int NegativeLearningWeight = -3;
    private readonly WindowsUserFeedbackService _feedback = new();
    private readonly DispatcherTimer _closeTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly DispatcherTimer _indexRefreshTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private readonly DocumentClassificationService _classificationService = new(new InvoiceDetectionService());
    private readonly CompanyDetectionService _companyDetection = new();
    private readonly PlaceDetectionService _placeDetection = new();
    private readonly DateDetectionService _dateDetection = new();
    private readonly InvoiceDetectionService _invoiceDetection = new();
    private readonly FileRenameSuggestionService _renameService = new();
    private readonly HighConfidenceAutoRenamePolicy _autoRenamePolicy = new();
    private readonly WindowsFileNamePolicy _fileNamePolicy = new();
    private readonly WindowsPathLengthPolicy _pathLengthPolicy = new();
    private readonly SafeFileMoveService _moveService = new();
    private readonly InMemoryLearningSettingsService _learningSettings = new();
    private readonly LocalLearningService _learningService = new(new InMemoryLearningRepository());
    private readonly InactivityReminderPolicy _reminderPolicy = new(TimeSpan.FromMinutes(2));
    private readonly DispatcherTimer _reminderTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    private readonly FolderScoringService _folderScoring = new(new TextNormalizer(), MaxDepth);
    private readonly string _oneDriveRoot;
    private readonly string _stateDirectory;
    private AtlasDropOptions _options = new();
    private List<FolderEntry> _folders = new();
    private List<SuggestionOption> _suggestions = new();
    private Dictionary<string, int> _learning = new(StringComparer.OrdinalIgnoreCase);
    private string? _activePath;
    private string? _proposedFolder;
    private AnalysisSnapshot? _analysis;
    private DocumentClassificationResult _classification = new(DocumentType.Unknown, 0d, Array.Empty<string>());
    private PendingMove? _pendingMove;
    private DecisionPath _decisionPath;
    private string? _initialSuggestedFolder;
    private string? _lockedSourcePath;
    private nint? _trackedExplorerHwnd;
    private long _explorerOpenRequestId;
    private readonly HashSet<string> _rejectedDestinations = new(StringComparer.OrdinalIgnoreCase);
    private bool _learningCommitted;
    private bool _moveInProgress;
    private bool _busy;
    private bool _isUserTyping;
    private DateTime _lastActivityUtc = DateTime.UtcNow;
    private bool _reminderShown;
    private FileSystemWatcher? _indexWatcher;

    public MainWindow()
    {
        InitializeComponent();
        _oneDriveRoot = FindOneDriveRoot();
        _options = new AtlasDropOptions { OneDriveRoot = _oneDriveRoot, MaxSuggestedDepth = MaxDepth };
        _stateDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AtlasDrop");
        Directory.CreateDirectory(_stateDirectory);
        LoadLearning();
        _folders = LoadOrBuildIndex();
        RenameTextBox.TextChanged += OnUserTextChanged;
        LearningEnabledCheckBox.Checked += OnLearningEnabledChanged;
        LearningEnabledCheckBox.Unchecked += OnLearningEnabledChanged;
        ResetLearningButton.Click += OnResetLearningClicked;
        _reminderTimer.Tick += OnReminderTick;
        _reminderTimer.Start();
        _feedback.PlayLaunchSound();
        _closeTimer.Tick += (_, _) => { _closeTimer.Stop(); Hide(); ResetOperation(); };
        _indexRefreshTimer.Tick += async (_, _) =>
        {
            _indexRefreshTimer.Stop();
            _folders = await Task.Run(BuildIndex);
            SaveIndex(_folders);
        };
        StartIndexWatcher();
        Loaded += (_, _) => PositionTopRight();
        Closing += (_, e) =>
        {
            e.Cancel = true;
            if (_pendingMove is not null)
            {
                StatusText.Text = "Réponds à la question de conformité avant de fermer.";
                Activate();
                return;
            }
            Hide();
            ResetOperation();
        };
    }

    public void SignalMiddleClickDetected() => StatusText.Text = "Clic détecté — analyse en cours…";

    public void SignalMiddleClickResolutionFailed()
    {
        ResetOperation();
        ItemNameText.Text = "Fichier non détecté";
        ProposedPathText.Text = "—";
        ConfidenceText.Text = "Aucun fichier n’a été déplacé.";
        StatusText.Text = "Impossible d’identifier ce fichier. Réessaie ou utilise « Ranger avec Atlas Drop ».";
        PositionTopRight();
    }

    public async void ActivateFile(string filePath)
    {
        if (_busy || string.IsNullOrWhiteSpace(filePath)) return;
        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath) && !Directory.Exists(fullPath)) return;

        if (IsProtectedOneDriveSource(fullPath))
        {
            ResetOperation();
            ItemNameText.Text = Path.GetFileName(fullPath);
            ProposedPathText.Text = "—";
            ConfidenceText.Text = "Aucun élément n’a été déplacé.";
            StatusText.Text = "Dossier principal OneDrive protégé — aucun déplacement.";
            Show();
            Activate();
            PositionTopRight();
            return;
        }

        _busy = true;
        _activePath = fullPath;
        _lastActivityUtc = DateTime.UtcNow;
        _reminderShown = false;
        _pendingMove = null;
        _decisionPath = DecisionPath.None;
        _initialSuggestedFolder = null;
        _lockedSourcePath = null;
        _trackedExplorerHwnd = null;
        _explorerOpenRequestId++;
        _rejectedDestinations.Clear();
        _learningCommitted = false;
        _moveInProgress = false;
        ItemNameText.Text = Path.GetFileName(fullPath);
        var FileNameText = ItemNameText;
        FileNameText.Text = Path.GetFileName(fullPath);
        ExplorerRefinementPanel.Visibility = Visibility.Collapsed;
        PostMovePanel.Visibility = Visibility.Collapsed;
        DecisionButtons.Visibility = Visibility.Visible;
        MoveHereButton.Visibility = Visibility.Collapsed;
        StatusText.Visibility = Visibility.Visible;
        StatusText.Text = "Analyse en cours…";
        Show(); Activate(); PositionTopRight();

        await AnalyzeActiveFileAsync();
        await LoadAutomaticFolderCandidatesAsync();
        if (_analysis is null) return;
        _suggestions = BuildSuggestions(_analysis);
        SuggestionList.ItemsSource = _suggestions;
        SuggestionList.SelectedIndex = _suggestions.Count > 0 ? 0 : -1;
        YesButton.IsEnabled = _suggestions.Count > 0;
        NoButton.IsEnabled = _suggestions.Count > 0;

        if (_suggestions.Count == 0)
        {
            _proposedFolder = null;
            ProposedPathText.Text = "Aucun dossier assez fiable";
            ConfidenceText.Text = "Confiance inférieure à 30 % : utilise MAUVAIS DOSSIER pour parcourir OneDrive.";
        }

        PrepareRename(fullPath, _analysis, _suggestions.FirstOrDefault());
        StatusText.Text = "Valide explicitement avant tout déplacement.";
    }

    private async Task AnalyzeActiveFileAsync()
    {
        if (_activePath is null) return;
        _analysis = await AnalyzeItemAsync(_activePath);
    }

    private async Task LoadAutomaticFolderCandidatesAsync()
    {
        _folders = await Task.Run(() => EnumerateFoldersToDepth(_options.OneDriveRoot, _options.MaxSuggestedDepth));
    }

    private async Task<AnalysisSnapshot> AnalyzeItemAsync(string path)
    {
        var fragments = new List<string> { Path.GetFileNameWithoutExtension(path) };
        var extractedText = string.Empty;
        if (Directory.Exists(path))
        {
            try
            {
                fragments.AddRange(Directory.EnumerateFileSystemEntries(path)
                    .Take(300).Select(Path.GetFileName).Where(x => !string.IsNullOrWhiteSpace(x))!);
            }
            catch { }
        }
        else
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            try
            {
                TextExtractionResult? result = ext switch
                {
                    ".txt" or ".csv" or ".log" or ".md" => await new TextFileExtractor().ExtractAsync(path),
                    ".docx" => await new DocxTextExtractor().ExtractAsync(path),
                    ".xlsx" => await new XlsxTextExtractor().ExtractAsync(path),
                    ".pptx" => await new PptxTextExtractor().ExtractAsync(path),
                    ".pdf" => await new PdfTextExtractor().ExtractAsync(path),
                    _ => null
                };
                if (!string.IsNullOrWhiteSpace(result?.Text))
                {
                    extractedText = result.Text[..Math.Min(result.Text.Length, 100_000)];
                    fragments.Add(extractedText);
                }
            }
            catch { }
        }

        var combined = string.Join(' ', fragments);
        var classification = _classificationService.Classify(Path.GetFileName(path), extractedText);
        _classification = classification;
        var companies = _companyDetection.Detect(combined).Select(x => x.Name).ToList();
        var inferredCompany = DetectBusinessPhrase(combined);
        if (!string.IsNullOrWhiteSpace(inferredCompany) && !companies.Contains(inferredCompany, StringComparer.OrdinalIgnoreCase))
            companies.Add(inferredCompany);
        var places = _placeDetection.Detect(combined)
            .Select(x => CleanDetectedPlace(x.Name, companies))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var dates = _dateDetection.Detect(combined);
        var tokens = Tokenize(combined).Where(x => !x.Equals("document", StringComparison.OrdinalIgnoreCase)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var years = dates.Where(x => x.Value is not null).Select(x => x.Value!.Value.Year).Distinct().ToArray();
        var fallbackLabel = _classification.Type != DocumentType.Unknown ? _classification.Type.ToString() : "Document";
        if (fallbackLabel != "Document") tokens.Add(fallbackLabel);

        return new AnalysisSnapshot(combined, tokens, classification, places, companies, dates, years);
    }

    private List<SuggestionOption> BuildSuggestions(AnalysisSnapshot analysis)
    {
        var context = new FileSuggestionContext(
            Path.GetFileName(_activePath ?? string.Empty),
            analysis.Text,
            analysis.Classification.Type,
            analysis.Tokens.ToArray(),
            analysis.Places,
            analysis.Companies,
            analysis.Years,
            string.Join('|', analysis.Tokens.Take(8)));

        return _folders
            .Select(folder =>
            {
                var relative = Path.GetRelativePath(_oneDriveRoot, folder.Path);
                var candidate = new FolderCandidate(
                    folder.Path,
                    Path.GetFileName(folder.Path),
                    folder.Path,
                    folder.Depth,
                    IsExcludedFolder(relative),
                    IsGenericFolder(Path.GetFileName(folder.Path)) ? 0.20 : 0.70,
                    TryGetLastWrite(folder.Path),
                    0,
                    folder.Tokens.ToArray(),
                    _placeDetection.Detect(relative).Select(x => x.Name).ToArray(),
                    _companyDetection.Detect(relative).Select(x => x.Name).ToArray(),
                    folder.Tokens.Select(x => int.TryParse(x, out var year) ? year : 0).Where(x => x is >= 1900 and <= 2100).ToArray(),
                    new Dictionary<DocumentType, int>());
                var scored = _folderScoring.Score(candidate, context);
                var learnedValues = analysis.Tokens
                    .Where(token => _learning.TryGetValue(LearningKey(token, folder.Path), out _))
                    .Select(token => _learning[LearningKey(token, folder.Path)])
                    .ToArray();
                var learned = learnedValues.Length == 0 ? 0d : learnedValues.Average();
                var score = Math.Clamp(scored.Score + learned * 0.03, 0d, 1d);
                var reason = scored.Reasons.FirstOrDefault() ?? "correspondance du dossier";
                return new SuggestionOption(folder.Path, score, reason);
            })
            .Where(x => x.Score >= 0.30)
            .GroupBy(x => x.FullPath, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.Score).First())
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.FullPath, StringComparer.OrdinalIgnoreCase)
            .Take(1)
            .ToList();
    }

    private void OnSuggestionSelected(object sender, SelectionChangedEventArgs e)
    {
        if (SuggestionList.SelectedItem is not SuggestionOption option) return;
        _proposedFolder = option.FullPath;
        ProposedPathText.Text = ToOneDriveDisplayPath(option.FullPath);
        ConfidenceText.Text = $"Confiance {option.Score:P0} — {option.Reason}";
        YesButton.IsEnabled = true;
        if (_analysis is not null && _activePath is not null) PrepareRename(_activePath, _analysis, option);
    }

    private void PrepareRename(string path, AnalysisSnapshot analysis, SuggestionOption? option)
    {
        if (Directory.Exists(path))
        {
            RenamePanel.Visibility = Visibility.Collapsed;
            AutoRenameCheckBox.IsChecked = false;
            return;
        }

        RenamePanel.Visibility = Visibility.Visible;
        var date = analysis.Dates.FirstOrDefault();
        var exactDate = date?.Precision == DetectedDatePrecision.Day ? date.Value : null;
        var year = date?.Value?.Year;
        var month = date?.Precision is DetectedDatePrecision.Month or DetectedDatePrecision.Day ? date.Value?.Month : null;
        var invoice = _invoiceDetection.Detect(analysis.Text);
        var context = new FileRenameContext(
            Path.GetFileName(path),
            analysis.Classification.Type,
            exactDate,
            year,
            month,
            analysis.Places.FirstOrDefault(),
            analysis.Companies.FirstOrDefault(),
            null,
            invoice.InvoiceNumber);
        var suggestion = _renameService.Suggest(context);
        var confidence = new SuggestionConfidenceService().Evaluate(option?.Score ?? 0d);
        var decision = _autoRenamePolicy.Decide(suggestion, confidence, userEnabledAutoRename: true);
        RenameTextBox.Text = suggestion.ProposedFileName;
        AutoRenameCheckBox.IsChecked = decision.ShouldApply;
        AutoRenameStatusText.Text = suggestion.Changed
            ? $"Proposition modifiable — {decision.Reason}"
            : "Nom actuel conservé : aucune amélioration fiable.";
    }

    private async void OnYes(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_proposedFolder)) return;
        _decisionPath = DecisionPath.Exact;
        _initialSuggestedFolder = _proposedFolder;
        await ExecuteMoveOnceAsync(_proposedFolder);
    }

    private async void OnNo(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_proposedFolder)) return;
        _decisionPath = DecisionPath.GoodBranch;
        _initialSuggestedFolder = _proposedFolder;
        await EnterExplorerRefinementModeAsync(_proposedFolder);
    }

    private async void OnChoose(object sender, RoutedEventArgs e)
    {
        _decisionPath = DecisionPath.WrongFolder;
        _initialSuggestedFolder = _proposedFolder;
        await EnterExplorerRefinementModeAsync(_oneDriveRoot);
    }

    private async Task EnterExplorerRefinementModeAsync(string startingFolder)
    {
        if (_activePath is null) return;

        _lockedSourcePath = _activePath;
        _trackedExplorerHwnd = null;
        SuggestionPanel.Visibility = Visibility.Collapsed;
        RenamePanel.Visibility = Visibility.Collapsed;
        ExplorerRefinementPanel.Visibility = Visibility.Visible;
        DecisionButtons.Visibility = Visibility.Collapsed;
        MoveHereButton.Content = "DÉPOSER ICI";
        MoveHereButton.IsEnabled = false;
        MoveHereButton.Visibility = Visibility.Visible;
        TrackedExplorerDestinationText.Text = ToOneDriveDisplayPath(startingFolder);
        StatusText.Text = "Source verrouillée. Ouverture de l’Explorateur — aucun déplacement effectué.";

        _trackedExplorerHwnd = await OpenExplorerAndTrackAsync(startingFolder);
        MoveHereButton.IsEnabled = _trackedExplorerHwnd is not null;
        StatusText.Text = _trackedExplorerHwnd is null
            ? "Fenêtre Explorateur introuvable. Aucun déplacement effectué."
            : "Navigue dans cette fenêtre Explorateur, puis clique DÉPOSER ICI.";
    }

    private async void OnMoveHere(object sender, RoutedEventArgs e)
    {
        if (_decisionPath is not (DecisionPath.GoodBranch or DecisionPath.WrongFolder))
        {
            StatusText.Text = "Aucune destination Explorer n’est en cours d’affinement.";
            return;
        }

        if (_lockedSourcePath is null ||
            !string.Equals(_activePath, _lockedSourcePath, StringComparison.OrdinalIgnoreCase) ||
            (!File.Exists(_lockedSourcePath) && !Directory.Exists(_lockedSourcePath)))
        {
            StatusText.Text = "La source verrouillée n’est plus disponible. Aucun déplacement effectué.";
            return;
        }

        if (_trackedExplorerHwnd is not nint explorerHwnd ||
            !TryGetExplorerPathByHwnd(explorerHwnd, out var destination))
        {
            StatusText.Text = "La fenêtre Explorateur suivie est fermée ou inaccessible. Aucun déplacement effectué.";
            return;
        }

        var depth = GetDepth(_oneDriveRoot, destination);
        if (!Directory.Exists(destination) || !IsAllowedDestination(destination) || depth < 1 || depth > MaxDepth)
        {
            StatusText.Text = "Destination refusée : choisis un dossier OneDrive entre les niveaux 0 et 4.";
            return;
        }

        var displayPath = ToOneDriveDisplayPath(destination);
        TrackedExplorerDestinationText.Text = displayPath;
        ProposedPathText.Text = displayPath;
        StatusText.Text = $"Destination vérifiée : {displayPath}. Déplacement sécurisé…";
        await Dispatcher.Yield(DispatcherPriority.Render);
        await ExecuteMoveOnceAsync(destination);
    }

    private async Task ExecuteMoveOnceAsync(string destination)
    {
        if (_moveInProgress) return;
        _moveInProgress = true;
        try
        {
            await MoveAsync(destination);
        }
        finally
        {
            _moveInProgress = false;
        }
    }

    private void OnUserTextChanged(object sender, TextChangedEventArgs e)
    {
        _isUserTyping = true;
        _lastActivityUtc = DateTime.UtcNow;
        _reminderShown = false;
        _ = Task.Delay(900).ContinueWith(_ => Dispatcher.BeginInvoke(() => _isUserTyping = false));
    }

    private void OnLearningEnabledChanged(object sender, RoutedEventArgs e)
    {
        _learningSettings.SetEnabled(LearningEnabledCheckBox.IsChecked == true);
        LearningStatusText.Text = _learningSettings.IsEnabled ? "Apprentissage actif" : "Apprentissage désactivé";
    }

    private async void OnResetLearningClicked(object sender, RoutedEventArgs e)
    {
        await _learningService.ClearAsync();
        _learning.Clear();
        try { File.Delete(Path.Combine(_stateDirectory, "learning-v108.json")); } catch { }
        LearningStatusText.Text = "Apprentissage effacé";
    }

    private void OnReminderTick(object? sender, EventArgs e)
    {
        if (!_busy || !_reminderPolicy.ShouldRemind(DateTime.UtcNow, _lastActivityUtc, _isUserTyping, _reminderShown)) return;
        _reminderShown = true;
        _feedback.PlayReminderSound();
        StatusText.Text = "Atlas Drop attend ta décision. Aucun déplacement n'a été effectué.";
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        if (_pendingMove is not null) return;
        Hide();
        ResetOperation();
    }

    private async Task MoveAsync(string? destination)
    {
        if (string.IsNullOrWhiteSpace(_activePath) || string.IsNullOrWhiteSpace(destination)) return;
        var destinationDepth = GetDepth(_oneDriveRoot, destination);
        if (!IsAllowedDestination(destination) || !Directory.Exists(destination) || destinationDepth < 1 || destinationDepth > MaxDepth)
        {
            StatusText.Text = "Destination OneDrive invalide ou au-delà du niveau 4.";
            return;
        }

        if (Directory.Exists(_activePath))
        {
            var count = await Task.Run(() => CountItems(_activePath));
            if (count > 10 && MessageBox.Show($"Ce dossier contient {count} éléments, sous-dossiers compris. Le déplacer ?", "Confirmation Atlas Drop", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
        }

        try
        {
            var source = _activePath;
            var sourceIsDirectory = Directory.Exists(source);
            var targetName = Path.GetFileName(source);
            if (!sourceIsDirectory && AutoRenameCheckBox.IsChecked == true && !string.IsNullOrWhiteSpace(RenameTextBox.Text))
            {
                var edited = RenameTextBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(Path.GetExtension(edited))) edited += Path.GetExtension(source);
                var safeName = _fileNamePolicy.Sanitize(edited);
                if (!safeName.IsValid) { StatusText.Text = "Le nom proposé est invalide sous Windows."; return; }
                targetName = safeName.SafeFileName;
            }

            var requestedTarget = Path.Combine(destination, targetName);
            var pathCheck = _pathLengthPolicy.CheckDestination(destination, targetName);
            PathLengthStatusText.Text = pathCheck.Message;
            if (!pathCheck.IsSafe) { StatusText.Text = pathCheck.Message; return; }
            var overwrite = false;
            string target;
            if (!sourceIsDirectory && File.Exists(requestedTarget))
            {
                var choice = MessageBox.Show(
                    "Un fichier du même nom existe déjà.\n\nOUI = remplacer\nNON = renommer automatiquement (recommandé)\nANNULER = ne rien déplacer",
                    "Doublon détecté", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning, MessageBoxResult.No);
                if (choice == MessageBoxResult.Cancel) return;
                overwrite = choice == MessageBoxResult.Yes;
                target = overwrite ? requestedTarget : AvailableTarget(destination, targetName, false);
            }
            else target = AvailableTarget(destination, targetName, sourceIsDirectory);

            var originalCreation = File.Exists(source) ? File.GetCreationTimeUtc(source) : DateTime.MinValue;
            var originalWrite = File.Exists(source) ? File.GetLastWriteTimeUtc(source) : DateTime.MinValue;
            StatusText.Text = "Déplacement sécurisé…";
            IsEnabled = false;
            if (sourceIsDirectory)
            {
                await Task.Run(() => Directory.Move(source, target));
            }
            else if (overwrite)
            {
                await Task.Run(() => File.Move(source, target, true));
            }
            else
            {
                var moveResult = await _moveService.MoveAsync(new SafeMoveRequest(
                    _oneDriveRoot, source, destination, Path.GetFileName(target)));
                if (!moveResult.Success || string.IsNullOrWhiteSpace(moveResult.DestinationPath))
                    throw new IOException(moveResult.Message);
                target = moveResult.DestinationPath;
            }
            if (!File.Exists(target) && !Directory.Exists(target)) throw new IOException("Vérification du déplacement impossible.");
            if (File.Exists(target))
            {
                File.SetCreationTimeUtc(target, originalCreation);
                File.SetLastWriteTimeUtc(target, originalWrite);
            }

            _pendingMove = new PendingMove(source, target, destination, sourceIsDirectory);
            _activePath = target;
            SaveLastMove(_pendingMove, "PENDING_CONFIRMATION");
            _feedback.PlaySuccessSound();
            IsEnabled = true;
            ExplorerRefinementPanel.Visibility = Visibility.Collapsed;
            DecisionButtons.Visibility = Visibility.Collapsed;
            MoveHereButton.Visibility = Visibility.Collapsed;
            RenamePanel.Visibility = Visibility.Collapsed;
            PostMovePanel.Visibility = Visibility.Visible;
            StatusText.Text = "Déplacement vérifié. Confirme maintenant le classement.";
        }
        catch (Exception ex)
        {
            IsEnabled = true;
            StatusText.Text = "Déplacement impossible : " + ex.Message;
        }
    }

    private void OnClassificationConfirmed(object sender, RoutedEventArgs e)
    {
        if (_pendingMove is null) return;
        ApplyConfirmedLearning(_pendingMove.Destination);
        SaveLastMove(_pendingMove, "CONFIRMED");
        _pendingMove = null;
        PostMovePanel.Visibility = Visibility.Collapsed;
        StatusText.Text = "Classement confirmé et apprentissage enregistré.";
        _closeTimer.Start();
    }

    private async void OnClassificationRejected(object sender, RoutedEventArgs e)
    {
        if (_pendingMove is null) return;
        var move = _pendingMove;
        IsEnabled = false;
        StatusText.Text = "Restauration du fichier avant correction…";
        try
        {
            var restored = await Task.Run(() => RestoreMove(move));
            _pendingMove = null;
            _activePath = restored;
            SaveLastMove(move, "REJECTED_AND_RESTORED");
            IsEnabled = true;
            PostMovePanel.Visibility = Visibility.Collapsed;
            _rejectedDestinations.Add(move.Destination);
            _decisionPath = DecisionPath.WrongFolder;
            _initialSuggestedFolder = move.Destination;
            StatusText.Text = "Classement annulé et source restaurée. Choisis le bon dossier.";
            await EnterExplorerRefinementModeAsync(_oneDriveRoot);
        }
        catch (Exception ex)
        {
            IsEnabled = true;
            StatusText.Text = "Restauration impossible : " + ex.Message + ". Aucun apprentissage enregistré.";
        }
    }

    private static string RestoreMove(PendingMove move)
    {
        if (!File.Exists(move.Target) && !Directory.Exists(move.Target)) throw new IOException("Élément déplacé introuvable.");
        var sourceDirectory = Path.GetDirectoryName(move.Source) ?? throw new IOException("Dossier source invalide.");
        Directory.CreateDirectory(sourceDirectory);
        var restored = (!File.Exists(move.Source) && !Directory.Exists(move.Source))
            ? move.Source
            : AvailableTarget(sourceDirectory, Path.GetFileName(move.Source), move.IsDirectory);
        if (move.IsDirectory) Directory.Move(move.Target, restored);
        else File.Move(move.Target, restored);
        return restored;
    }

    private List<FolderEntry> EnumerateFoldersToDepth(string root, int maxDepth) => EnumerateFoldersSafe(root, maxDepth);

    private List<FolderEntry> EnumerateFoldersSafe(string root, int maxDepth)
    {
        var result = new List<FolderEntry>();
        if (!Directory.Exists(root)) return result;
        var queue = new Queue<(string Path, int Depth)>();
        queue.Enqueue((root, 0));
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.Depth >= maxDepth) continue;
            try
            {
                foreach (var child in Directory.EnumerateDirectories(current.Path))
                {
                    if (current.Depth == 0 && !AllowedRootFolderNames.Contains(Path.GetFileName(child))) continue;
                    var relative = Path.GetRelativePath(root, child);
                    if (IsExcludedFolder(relative)) continue;
                    var depth = current.Depth + 1;
                    result.Add(new FolderEntry(child, depth, Tokenize(relative)));
                    queue.Enqueue((child, depth));
                }
            }
            catch { }
        }
        return result;
    }

    private static int GetDepth(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        if (string.IsNullOrWhiteSpace(relative) || relative == ".") return 0;
        return relative.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static TreeViewItem NewTreeItem(string path, string? header = null) =>
        new() { Header = header ?? Path.GetFileName(path), Tag = path };

    private List<FolderEntry> LoadOrBuildIndex()
    {
        var cache = Path.Combine(_stateDirectory, "folder-index-v112.json");
        try
        {
            if (File.Exists(cache) && DateTime.UtcNow - File.GetLastWriteTimeUtc(cache) < TimeSpan.FromHours(12))
                return JsonSerializer.Deserialize<List<FolderEntry>>(File.ReadAllText(cache)) ?? new();
        }
        catch { }
        var result = BuildIndex();
        SaveIndex(result);
        return result;
    }

    private List<FolderEntry> BuildIndex()
    {
        var result = new List<FolderEntry>();
        if (Directory.Exists(_oneDriveRoot))
        {
            var queue = new Queue<(string Path, int Depth)>(); queue.Enqueue((_oneDriveRoot, 0));
            while (queue.Count > 0)
            {
                var current = queue.Dequeue(); if (current.Depth >= MaxDepth) continue;
                try
                {
                    foreach (var child in Directory.EnumerateDirectories(current.Path))
                    {
                        if (current.Depth == 0 && !AllowedRootFolderNames.Contains(Path.GetFileName(child))) continue;
                        var depth = current.Depth + 1;
                        var signals = new List<string> { Path.GetRelativePath(_oneDriveRoot, child) };
                        try { signals.AddRange(Directory.EnumerateFileSystemEntries(child).Take(50).Select(Path.GetFileName)!); } catch { }
                        result.Add(new FolderEntry(child, depth, Tokenize(string.Join(' ', signals))));
                        queue.Enqueue((child, depth));
                    }
                }
                catch { }
            }
        }
        return result;
    }

    private void SaveIndex(List<FolderEntry> folders)
    {
        try { File.WriteAllText(Path.Combine(_stateDirectory, "folder-index-v112.json"), JsonSerializer.Serialize(folders)); } catch { }
    }

    private void StartIndexWatcher()
    {
        if (!Directory.Exists(_oneDriveRoot)) return;
        try
        {
            _indexWatcher = new FileSystemWatcher(_oneDriveRoot)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName
            };
            FileSystemEventHandler changed = (_, _) => Dispatcher.BeginInvoke(() => { _indexRefreshTimer.Stop(); _indexRefreshTimer.Start(); });
            RenamedEventHandler renamed = (_, _) => Dispatcher.BeginInvoke(() => { _indexRefreshTimer.Stop(); _indexRefreshTimer.Start(); });
            _indexWatcher.Created += changed; _indexWatcher.Deleted += changed; _indexWatcher.Renamed += renamed;
            _indexWatcher.EnableRaisingEvents = true;
        }
        catch { }
    }

    private void LoadLearning()
    {
        try
        {
            var file = Path.Combine(_stateDirectory, "learning-v108.json");
            if (File.Exists(file)) _learning = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(file)) ?? _learning;
        }
        catch { }
    }

    private void ApplyConfirmedLearning(string finalDestination)
    {
        if (_learningCommitted) return;

        var signals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [finalDestination] = StrongPositiveLearningWeight
        };

        if (_decisionPath == DecisionPath.GoodBranch &&
            !string.IsNullOrWhiteSpace(_initialSuggestedFolder) &&
            IsSameOrDescendant(_initialSuggestedFolder, finalDestination))
        {
            signals[_initialSuggestedFolder] = signals.TryGetValue(_initialSuggestedFolder, out var existing)
                ? Math.Max(existing, StrongPositiveLearningWeight)
                : WeakPositiveLearningWeight;
        }
        else if (_decisionPath == DecisionPath.WrongFolder &&
                 !string.IsNullOrWhiteSpace(_initialSuggestedFolder) &&
                 !SamePath(_initialSuggestedFolder, finalDestination))
        {
            signals[_initialSuggestedFolder] = NegativeLearningWeight;
        }

        foreach (var rejected in _rejectedDestinations)
        {
            if (!SamePath(rejected, finalDestination))
                signals[rejected] = NegativeLearningWeight;
        }

        RecordLearningBatch(signals);
        _learningCommitted = true;
    }

    private void RecordLearningBatch(IReadOnlyDictionary<string, int> signals)
    {
        if (!_learningSettings.IsEnabled || signals.Count == 0) return;

        var tokens = (_analysis?.Tokens ?? Tokenize(Path.GetFileNameWithoutExtension(_activePath ?? string.Empty)))
            .OrderBy(token => token, StringComparer.Ordinal)
            .Take(16)
            .ToArray();

        foreach (var signal in signals)
        {
            foreach (var token in tokens)
            {
                var key = LearningKey(token, signal.Key);
                _learning.TryGetValue(key, out var score);
                _learning[key] = Math.Clamp(score + signal.Value, -20, 50);
            }
        }

        try
        {
            File.WriteAllText(
                Path.Combine(_stateDirectory, "learning-v108.json"),
                JsonSerializer.Serialize(_learning));
        }
        catch { }
    }

    private static bool IsSameOrDescendant(string parent, string candidate)
    {
        if (SamePath(parent, candidate)) return true;
        var normalizedParent = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var normalizedCandidate = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        return normalizedCandidate.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
    }

    private static bool SamePath(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    private void SaveLastMove(PendingMove move, string status)
    {
        try
        {
            var audit = new { move.Source, move.Target, move.Destination, Status = status, TimestampUtc = DateTime.UtcNow };
            File.WriteAllText(Path.Combine(_stateDirectory, "last-move-v108.json"), JsonSerializer.Serialize(audit));
        }
        catch { }
    }

    private static string LearningKey(string token, string folder) => token.ToLowerInvariant() + "=>" + folder.ToLowerInvariant();

    private static HashSet<string> Tokenize(string text) => text.ToLowerInvariant()
        .Split(new[] { ' ', '\\', '/', '-', '_', '.', '(', ')', '[', ']', '&', ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
        .Where(x => x.Length >= 3).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private string FindOneDriveRoot()
    {
        var configured = @"C:\Users\fchelli\OneDrive - ALTEDIS";
        if (Directory.Exists(configured)) return configured;
        var env = Environment.GetEnvironmentVariable("OneDriveCommercial") ?? Environment.GetEnvironmentVariable("OneDrive");
        if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env)) return env;
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Directory.EnumerateDirectories(profile, "OneDrive*", SearchOption.TopDirectoryOnly).FirstOrDefault() ?? configured;
    }

    private bool IsUnderRoot(string path)
    {
        var root = Path.GetFullPath(_oneDriveRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase) || string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsProtectedOneDriveSource(string path)
    {
        if (!Directory.Exists(path) || !IsUnderRoot(path)) return false;
        return GetDepth(_oneDriveRoot, path) is 0 or 1;
    }

    private bool IsAllowedDestination(string path)
    {
        if (!IsUnderRoot(path)) return false;
        var relative = Path.GetRelativePath(_oneDriveRoot, path);
        if (string.IsNullOrWhiteSpace(relative) || relative == ".") return false;
        var firstSegment = relative.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return firstSegment is not null && AllowedRootFolderNames.Contains(firstSegment);
    }

    private static bool IsGenericFolder(string name) => name.Trim().ToLowerInvariant() is "divers" or "documents" or "fichiers" or "temp" or "tmp" or "autres";

    private static bool IsExcludedFolder(string relativePath)
    {
        var excluded = new HashSet<string>(new[] { "Bureau", "Desktop", "Documents", "Images", "Pictures", "99 - Archives", "$RECYCLE.BIN", "Temp", "Cache" }, StringComparer.OrdinalIgnoreCase);
        return relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(excluded.Contains);
    }

    private static DateTime? TryGetLastWrite(string path)
    {
        try { return Directory.GetLastWriteTimeUtc(path); } catch { return null; }
    }

    private static string CleanDetectedPlace(string place, IReadOnlyCollection<string> companies)
    {
        var cleaned = place.Trim();
        foreach (var company in companies.Where(x => !string.IsNullOrWhiteSpace(x)).OrderByDescending(x => x.Length))
        {
            var index = cleaned.IndexOf(company, StringComparison.OrdinalIgnoreCase);
            if (index >= 0) cleaned = cleaned[..index].Trim(' ', '-', ',', ';');
        }
        return cleaned;
    }

    private static string? DetectBusinessPhrase(string text)
    {
        var match = Regex.Match(text, @"\b(?<company>[A-ZÀ-ÖØ-Þ][A-ZÀ-ÖØ-Þ0-9&'’.-]{1,30}(?:\s+[A-ZÀ-ÖØ-Þ][A-ZÀ-ÖØ-Þ0-9&'’.-]{1,30}){0,3}\s+(?:GESTION|INVEST|IMMOBILIER|ASSURANCES?|BANQUE))\b", RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["company"].Value.Trim() : null;
    }

    private static string AvailableTarget(string destination, string name, bool directory)
    {
        var target = Path.Combine(destination, name);
        if (!File.Exists(target) && !Directory.Exists(target)) return target;
        var stem = directory ? name : Path.GetFileNameWithoutExtension(name);
        var extension = directory ? "" : Path.GetExtension(name);
        for (var i = 2; i < 10_000; i++)
        {
            target = Path.Combine(destination, $"{stem} ({i}){extension}");
            if (!File.Exists(target) && !Directory.Exists(target)) return target;
        }
        throw new IOException("Impossible de créer un nom disponible.");
    }

    private static int CountItems(string root)
    {
        var count = 0; var stack = new Stack<string>(); stack.Push(root);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            try { foreach (var entry in Directory.EnumerateFileSystemEntries(current)) { count++; if (Directory.Exists(entry)) stack.Push(entry); } } catch { }
        }
        return count;
    }

    private async Task<nint?> OpenExplorerAndTrackAsync(string folder)
    {
        var requestId = ++_explorerOpenRequestId;
        var before = ReadExplorerWindows();
        var beforeByHandle = before
            .GroupBy(item => item.Hwnd)
            .ToDictionary(group => group.Key, group => group.First());

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/n,\"{folder}\"") { UseShellExecute = true });
        }
        catch
        {
            return null;
        }

        for (var attempt = 0; attempt < 40; attempt++)
        {
            await Task.Delay(150);
            if (requestId != _explorerOpenRequestId) return null;

            var current = ReadExplorerWindows();
            var exactMatches = current
                .Where(item => PathsEqualSafe(item.Path, folder))
                .ToArray();

            var tracked = exactMatches.FirstOrDefault(item => !beforeByHandle.ContainsKey(item.Hwnd));
            tracked ??= exactMatches.FirstOrDefault(item =>
                beforeByHandle.TryGetValue(item.Hwnd, out var previous) &&
                !PathsEqualSafe(previous.Path, folder));

            if (tracked is null && attempt >= 10 && exactMatches.Length == 1)
                tracked = exactMatches[0];

            if (tracked is null) continue;
            PositionExplorerWindow(tracked.Hwnd);
            return tracked.Hwnd;
        }

        return null;
    }

    private static IReadOnlyList<ExplorerWindowSnapshot> ReadExplorerWindows()
    {
        var snapshots = new List<ExplorerWindowSnapshot>();
        var shellType = Type.GetTypeFromProgID("Shell.Application");
        if (shellType is null) return snapshots;

        dynamic? shell = null;
        dynamic? windows = null;
        try
        {
            shell = Activator.CreateInstance(shellType);
            windows = shell?.Windows();
            if (windows is null) return snapshots;

            var count = Convert.ToInt32(windows.Count);
            for (var index = 0; index < count; index++)
            {
                dynamic? window = null;
                try
                {
                    window = windows.Item(index);
                    if (window is null) continue;
                    var hwnd = (nint)Convert.ToInt64(window.HWND);
                    var path = ReadExplorerFolderPath((object)window);
                    if (hwnd != 0) snapshots.Add(new ExplorerWindowSnapshot(hwnd, path));
                }
                catch { }
                finally
                {
                    ReleaseComObject((object?)window);
                }
            }
        }
        catch { }
        finally
        {
            ReleaseComObject((object?)windows);
            ReleaseComObject((object?)shell);
        }

        return snapshots;
    }

    private static string? ReadExplorerFolderPath(object window)
    {
        object? document = null;
        object? folder = null;
        object? self = null;
        try
        {
            document = ((dynamic)window).Document;
            if (document is null) return null;
            folder = ((dynamic)document).Folder;
            if (folder is null) return null;
            self = ((dynamic)folder).Self;
            return self is null ? null : (string?)((dynamic)self).Path;
        }
        catch
        {
            return null;
        }
        finally
        {
            ReleaseComObject(self);
            ReleaseComObject(folder);
            ReleaseComObject(document);
        }
    }

    private static bool TryGetExplorerPathByHwnd(nint trackedHwnd, out string path)
    {
        var match = ReadExplorerWindows().FirstOrDefault(item => item.Hwnd == trackedHwnd);
        path = match?.Path ?? string.Empty;
        return !string.IsNullOrWhiteSpace(path);
    }

    private static bool PathsEqualSafe(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
        try
        {
            return SamePath(left, right);
        }
        catch
        {
            return false;
        }
    }

    private static void PositionExplorerWindow(nint hwnd)
    {
        var monitor = GetCursorMonitorPlacement();
        var area = monitor.WorkArea;
        MoveWindow(
            hwnd,
            area.Left + (int)((area.Right - area.Left) * .68),
            area.Top + (int)(8 * monitor.ScaleY),
            (int)((area.Right - area.Left) * .31),
            (int)((area.Bottom - area.Top) * .55),
            true);
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            try { Marshal.FinalReleaseComObject(value); } catch { }
        }
    }

    private void PositionTopRight()
    {
        var monitor = GetCursorMonitorPlacement();
        var area = monitor.WorkArea;
        var workWidthDip = (area.Right - area.Left) / monitor.ScaleX;
        var workHeightDip = (area.Bottom - area.Top) / monitor.ScaleY;
        var preferredRightGapDip = workWidthDip >= MinWidth + 134 ? 110d : 12d;
        var maxWidthDip = Math.Max(MinWidth, workWidthDip - preferredRightGapDip - 24);
        var maxHeightDip = Math.Max(MinHeight, workHeightDip - 16);

        Width = Math.Min(maxWidthDip, Math.Max(620, workWidthDip * .38));
        Height = Math.Min(maxHeightDip, Math.Max(560, workHeightDip * .78));

        var widthPixels = (int)Math.Round(Width * monitor.ScaleX);
        var heightPixels = (int)Math.Round(Height * monitor.ScaleY);
        var rightGapPixels = (int)Math.Round(preferredRightGapDip * monitor.ScaleX);
        var leftPixels = Math.Max(
            area.Left + (int)Math.Round(12 * monitor.ScaleX),
            area.Right - rightGapPixels - widthPixels);
        var topPixels = area.Top + (int)Math.Round(8 * monitor.ScaleY);

        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != 0)
        {
            MoveWindow(hwnd, leftPixels, topPixels, widthPixels, heightPixels, true);
            return;
        }

        var fallback = SystemParameters.WorkArea;
        Left = Math.Max(fallback.Left + 12, fallback.Right - preferredRightGapDip - Width);
        Top = fallback.Top + 8;
    }

    private static MonitorPlacement GetCursorMonitorPlacement()
    {
        try
        {
            if (!GetCursorPos(out var cursor)) return FallbackMonitorPlacement();
            var monitor = MonitorFromPoint(cursor, MonitorDefaultToNearest);
            if (monitor == 0) return FallbackMonitorPlacement();

            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(monitor, ref info)) return FallbackMonitorPlacement();

            var dpiX = 96u;
            var dpiY = 96u;
            try
            {
                if (GetDpiForMonitor(monitor, MonitorDpiTypeEffective, out var detectedX, out var detectedY) == 0)
                {
                    dpiX = detectedX;
                    dpiY = detectedY;
                }
            }
            catch { }

            return new MonitorPlacement(
                info.WorkArea,
                Math.Max(1d, dpiX / 96d),
                Math.Max(1d, dpiY / 96d));
        }
        catch
        {
            return FallbackMonitorPlacement();
        }
    }

    private static MonitorPlacement FallbackMonitorPlacement()
    {
        var area = SystemParameters.WorkArea;
        return new MonitorPlacement(
            new NativeRect
            {
                Left = (int)area.Left,
                Top = (int)area.Top,
                Right = (int)area.Right,
                Bottom = (int)area.Bottom
            },
            1d,
            1d);
    }

    private string ToOneDriveDisplayPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !IsUnderRoot(path))
            return Path.GetFileName(path);

        var relative = Path.GetRelativePath(_oneDriveRoot, path);
        if (string.IsNullOrWhiteSpace(relative) || relative == ".")
            return "OneDrive";

        var segments = relative.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);
        return "OneDrive › " + string.Join(" › ", segments);
    }

    private void ResetOperation()
    {
        _closeTimer.Stop();
        _explorerOpenRequestId++;
        _activePath = null; _proposedFolder = null; _analysis = null; _pendingMove = null; _busy = false;
        _decisionPath = DecisionPath.None; _initialSuggestedFolder = null; _lockedSourcePath = null; _trackedExplorerHwnd = null;
        _rejectedDestinations.Clear(); _learningCommitted = false; _moveInProgress = false;
        _suggestions.Clear(); SuggestionList.ItemsSource = null; Height = 690;
        ItemNameText.Text = "En attente d’un clic molette…"; ProposedPathText.Text = "—"; ConfidenceText.Text = "";
        TrackedExplorerDestinationText.Text = "Ouverture de l’Explorateur…";
        RenameTextBox.Text = ""; AutoRenameStatusText.Text = ""; AutoRenameCheckBox.IsChecked = false;
        ExplorerRefinementPanel.Visibility = Visibility.Collapsed; PostMovePanel.Visibility = Visibility.Collapsed;
        SuggestionPanel.Visibility = Visibility.Visible;
        DecisionButtons.Visibility = Visibility.Visible; MoveHereButton.Visibility = Visibility.Collapsed; RenamePanel.Visibility = Visibility.Visible;
        MoveHereButton.IsEnabled = false; YesButton.IsEnabled = false; NoButton.IsEnabled = false; IsEnabled = true;
    }

    public sealed record FolderEntry(string Path, int Depth, HashSet<string> Tokens);
    public sealed record SuggestionOption(string FullPath, double Score, string Reason)
    {
        public string DisplayText => $"{Score:P0} — {FullPath}";
    }
    private sealed record AnalysisSnapshot(
        string Text,
        HashSet<string> Tokens,
        DocumentClassificationResult Classification,
        IReadOnlyList<string> Places,
        IReadOnlyList<string> Companies,
        IReadOnlyList<DetectedDate> Dates,
        IReadOnlyList<int> Years);
    private sealed record PendingMove(string Source, string Target, string Destination, bool IsDirectory);
    private sealed record ExplorerWindowSnapshot(nint Hwnd, string? Path);
    private sealed record MonitorPlacement(NativeRect WorkArea, double ScaleX, double ScaleY);
    private enum DecisionPath { None, Exact, GoodBranch, WrongFolder }

    private const uint MonitorDefaultToNearest = 2;
    private const int MonitorDpiTypeEffective = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect MonitorArea;
        public NativeRect WorkArea;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo monitorInfo);

    [DllImport("Shcore.dll")]
    private static extern int GetDpiForMonitor(
        nint monitor,
        int dpiType,
        out uint dpiX,
        out uint dpiY);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MoveWindow(IntPtr window, int x, int y, int width, int height, bool repaint);
}
