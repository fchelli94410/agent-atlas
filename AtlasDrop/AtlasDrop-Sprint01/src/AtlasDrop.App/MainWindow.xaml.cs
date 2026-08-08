using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
    private readonly SafeFolderCreationService _folderCreationService = new();
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
    private string? _manualFolder;
    private AnalysisSnapshot? _analysis;
    private DocumentClassificationResult _classification = new(DocumentType.Unknown, 0d, Array.Empty<string>());
    private PendingMove? _pendingMove;
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
        SearchButton.Click += OnSearchClicked;
        SearchTextBox.KeyDown += OnSearchTextBoxKeyDown;
        SearchTextBox.TextChanged += OnUserTextChanged;
        RenameTextBox.TextChanged += OnUserTextChanged;
        CreateFolderButton.Click += OnCreateFolderClicked;
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

        _busy = true;
        _activePath = fullPath;
        _lastActivityUtc = DateTime.UtcNow;
        _reminderShown = false;
        _pendingMove = null;
        ItemNameText.Text = Path.GetFileName(fullPath);
        var FileNameText = ItemNameText;
        FileNameText.Text = Path.GetFileName(fullPath);
        ManualPanel.Visibility = Visibility.Collapsed;
        PostMovePanel.Visibility = Visibility.Collapsed;
        DecisionButtons.Visibility = Visibility.Visible;
        MoveHereButton.Visibility = Visibility.Collapsed;
        StatusText.Visibility = Visibility.Visible;
        StatusText.Text = "Analyse en cours…";
        Show(); Activate(); PositionTopRight();

        await AnalyzeActiveFileAsync();
        await LoadAutomaticFolderCandidatesAsync();
        _ = LoadDeepFoldersForManualSearchAsync();
        if (_analysis is null) return;
        _suggestions = BuildSuggestions(_analysis);
        SuggestionList.ItemsSource = _suggestions;
        SuggestionList.SelectedIndex = _suggestions.Count > 0 ? 0 : -1;
        YesButton.IsEnabled = _suggestions.Count > 0;

        if (_suggestions.Count == 0)
        {
            _proposedFolder = null;
            ProposedPathText.Text = "Aucun dossier assez fiable";
            ConfidenceText.Text = "Confiance inférieure à 30 % : choisis manuellement.";
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

    private async Task LoadDeepFoldersForManualSearchAsync()
    {
        await Task.Run(() => EnumerateFoldersSafe(_options.OneDriveRoot, Math.Max(_options.MaxSuggestedDepth, 12)));
        await Dispatcher.InvokeAsync(() => LearningStatusText.Text = "Recherche manuelle complète disponible");
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
                var learned = analysis.Tokens.Sum(token => _learning.TryGetValue(LearningKey(token, folder.Path), out var value) ? value : 0);
                var score = Math.Clamp(scored.Score + learned * 0.02, 0d, 1d);
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
        ProposedPathText.Text = option.FullPath;
        ConfidenceText.Text = $"Confiance {option.Score:P0} — {option.Reason}";
        YesButton.IsEnabled = true;
        OpenExplorer(option.FullPath);
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

    private async void OnYes(object sender, RoutedEventArgs e) => await MoveAsync(_proposedFolder);

    private void OnNo(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_proposedFolder)) return;
        var parent = Directory.GetParent(_proposedFolder)?.FullName;
        _proposedFolder = !string.IsNullOrWhiteSpace(parent) && IsUnderRoot(parent) ? parent : _oneDriveRoot;
        ProposedPathText.Text = _proposedFolder;
        ConfidenceText.Text = "Proposition remontée d’un niveau. Aucun déplacement effectué.";
        OpenExplorer(_proposedFolder);
    }

    private void OnChoose(object sender, RoutedEventArgs e)
    {
        BuildFolderTree();
        ManualPanel.Visibility = Visibility.Visible;
        DecisionButtons.Visibility = Visibility.Collapsed;
        MoveHereButton.Visibility = Visibility.Visible;
        Height = 780;
    }

    private void OnFolderSelected(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not TreeViewItem item || item.Tag is not string path) return;
        _manualFolder = path;
        SelectedManualDestinationText.Text = path;
        ProposedPathText.Text = path;
        ConfidenceText.Text = "Dossier choisi manuellement — clique DÉPLACER ICI pour confirmer.";
        OpenExplorer(path);
    }

    private async void OnMoveHere(object sender, RoutedEventArgs e) => await MoveAsync(_manualFolder);

    private void OnSearchClicked(object sender, RoutedEventArgs e) => RunManualSearch();

    private void OnSearchTextBoxKeyDown(object sender, KeyEventArgs e)
    {
        _isUserTyping = true;
        _lastActivityUtc = DateTime.UtcNow;
        if (e.Key == Key.Enter) RunManualSearch();
    }

    private void OnUserTextChanged(object sender, TextChangedEventArgs e)
    {
        _isUserTyping = true;
        _lastActivityUtc = DateTime.UtcNow;
        _reminderShown = false;
        _ = Task.Delay(900).ContinueWith(_ => Dispatcher.BeginInvoke(() => _isUserTyping = false));
    }

    private void RunManualSearch()
    {
        var terms = Tokenize(SearchTextBox.Text);
        var results = _folders
            .Where(x => terms.Count == 0 || terms.All(t => x.Path.Contains(t, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(x => x.Path.Length)
            .Take(50)
            .ToArray();
        SearchResultsList.ItemsSource = results;
        if (results.Length == 0) StatusText.Text = "Aucun dossier trouvé.";
        else StatusText.Text = $"{results.Length} dossier(s) trouvé(s).";
    }

    private void OnManualSearchResultSelected(object sender, SelectionChangedEventArgs e)
    {
        if (SearchResultsList.SelectedItem is not FolderEntry result) return;
        _manualFolder = result.Path;
        SelectedManualDestinationText.Text = result.Path;
        ProposedPathText.Text = result.Path;
        OpenExplorer(result.Path);
    }

    private async void OnCreateFolderClicked(object sender, RoutedEventArgs e)
    {
        var parent = _manualFolder ?? _oneDriveRoot;
        var request = new FolderCreationRequest(_oneDriveRoot, parent, NewFolderNameTextBox.Text);
        var result = await _folderCreationService.CreateAsync(request);
        StatusText.Text = result.Message;
        if (!result.Success || string.IsNullOrWhiteSpace(result.FullPath)) return;
        _manualFolder = result.FullPath;
        SelectedManualDestinationText.Text = result.FullPath;
        _folders.Add(new FolderEntry(result.FullPath, GetDepth(_oneDriveRoot, result.FullPath), Tokenize(result.FullPath)));
        BuildFolderTree();
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
        if (!IsUnderRoot(destination) || !Directory.Exists(destination)) { StatusText.Text = "Destination OneDrive invalide."; return; }

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
            ManualPanel.Visibility = Visibility.Collapsed;
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
        RecordLearning(_pendingMove.Destination, accepted: true);
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
            RenamePanel.Visibility = Directory.Exists(restored) ? Visibility.Collapsed : Visibility.Visible;
            BuildFolderTree();
            ManualPanel.Visibility = Visibility.Visible;
            MoveHereButton.Visibility = Visibility.Visible;
            StatusText.Text = "Classement annulé et source restaurée. Choisis le bon dossier.";
            OpenExplorer(Path.GetDirectoryName(restored) ?? _oneDriveRoot);
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

    private void BuildFolderTree()
    {
        FolderTree.Items.Clear();
        var root = NewTreeItem(_oneDriveRoot);
        FolderTree.Items.Add(root);
        var byPath = new Dictionary<string, TreeViewItem>(StringComparer.OrdinalIgnoreCase) { [_oneDriveRoot] = root };
        foreach (var folder in _folders.OrderBy(x => x.Depth).ThenBy(x => x.Path))
        {
            var item = NewTreeItem(folder.Path); byPath[folder.Path] = item;
            var parent = Directory.GetParent(folder.Path)?.FullName;
            (parent is not null && byPath.TryGetValue(parent, out var p) ? p.Items : root.Items).Add(item);
        }
        root.IsExpanded = true;
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

    private static int GetDepth(string root, string path) =>
        Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length;

    private static TreeViewItem NewTreeItem(string path) => new() { Header = Path.GetFileName(path), Tag = path };

    private List<FolderEntry> LoadOrBuildIndex()
    {
        var cache = Path.Combine(_stateDirectory, "folder-index-v108.json");
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
        try { File.WriteAllText(Path.Combine(_stateDirectory, "folder-index-v108.json"), JsonSerializer.Serialize(folders)); } catch { }
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

    private void RecordLearning(string folder, bool accepted)
    {
        if (!_learningSettings.IsEnabled) return;
        var tokens = _analysis?.Tokens ?? Tokenize(Path.GetFileNameWithoutExtension(_activePath ?? string.Empty));
        foreach (var token in tokens.Take(16))
        {
            var key = LearningKey(token, folder);
            _learning.TryGetValue(key, out var score); _learning[key] = Math.Clamp(score + (accepted ? 1 : -1), -5, 20);
        }
        try { File.WriteAllText(Path.Combine(_stateDirectory, "learning-v108.json"), JsonSerializer.Serialize(_learning)); } catch { }
    }

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

    private async void OpenExplorer(string folder)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/n,\"{folder}\"") { UseShellExecute = true });
            await Task.Delay(650);
            PositionExplorerWindow(folder);
        }
        catch { }
    }

    private static void PositionExplorerWindow(string folder)
    {
        var shellType = Type.GetTypeFromProgID("Shell.Application");
        if (shellType is null) return;
        dynamic? shell = null; dynamic? windows = null;
        try
        {
            shell = Activator.CreateInstance(shellType); windows = shell?.Windows();
            if (windows is null) return;
            for (var index = windows.Count - 1; index >= 0; index--)
            {
                dynamic? window = windows.Item(index);
                try
                {
                    var currentPath = (string?)window.Document?.Folder?.Self?.Path;
                    if (!string.Equals(Path.GetFullPath(currentPath ?? ""), Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase)) continue;
                    var area = SystemParameters.WorkArea;
                    MoveWindow((IntPtr)(long)window.HWND, (int)(area.Left + area.Width * .68), (int)area.Top + 8,
                        (int)(area.Width * .31), (int)(area.Height * .55), true);
                    break;
                }
                catch { }
                finally { if (window is not null && Marshal.IsComObject(window)) Marshal.FinalReleaseComObject(window); }
            }
        }
        catch { }
        finally
        {
            if (windows is not null && Marshal.IsComObject(windows)) Marshal.FinalReleaseComObject(windows);
            if (shell is not null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
        }
    }

    private void PositionTopRight()
    {
        var area = SystemParameters.WorkArea;
        Width = Math.Max(620, area.Width * .38); Height = Math.Max(560, area.Height * .78);
        Left = area.Right - Width - 12; Top = area.Top + 8;
    }

    private void ResetOperation()
    {
        _closeTimer.Stop();
        _activePath = null; _proposedFolder = null; _manualFolder = null; _analysis = null; _pendingMove = null; _busy = false;
        _suggestions.Clear(); SuggestionList.ItemsSource = null; Height = 690;
        ItemNameText.Text = "En attente d’un clic molette…"; ProposedPathText.Text = "—"; ConfidenceText.Text = "";
        RenameTextBox.Text = ""; AutoRenameStatusText.Text = ""; AutoRenameCheckBox.IsChecked = false;
        ManualPanel.Visibility = Visibility.Collapsed; PostMovePanel.Visibility = Visibility.Collapsed;
        DecisionButtons.Visibility = Visibility.Visible; MoveHereButton.Visibility = Visibility.Collapsed; RenamePanel.Visibility = Visibility.Visible;
        YesButton.IsEnabled = false; IsEnabled = true;
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

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MoveWindow(IntPtr window, int x, int y, int width, int height, bool repaint);
}
