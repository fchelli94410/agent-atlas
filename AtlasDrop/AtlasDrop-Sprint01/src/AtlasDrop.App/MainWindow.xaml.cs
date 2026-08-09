using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
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
    private readonly WindowsFileNamePolicy _fileNamePolicy = new();
    private readonly WindowsPathLengthPolicy _pathLengthPolicy = new();
    private readonly SafeFileMoveService _moveService = new();
    private readonly InMemoryLearningSettingsService _learningSettings = new();
    private readonly LocalLearningService _learningService = new(new InMemoryLearningRepository());
    private readonly InactivityReminderPolicy _reminderPolicy = new(TimeSpan.FromMinutes(2));
    private readonly DispatcherTimer _reminderTimer = new() { Interval = TimeSpan.FromSeconds(10) };
    private readonly DispatcherTimer _explorerPathTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly DispatcherTimer _undoTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private int _undoSecondsRemaining;
    private bool _preparingRename;
    private LocalVoiceExplanationService? _voiceService;
    private string? _pendingVoiceExplanation;
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
    private bool _compactExplorerMode;

    public MainWindow()
    {
        InitializeComponent();
        _oneDriveRoot = FindOneDriveRoot();
        _options = new AtlasDropOptions { OneDriveRoot = _oneDriveRoot, MaxSuggestedDepth = MaxDepth };
        _stateDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AtlasDrop");
        Directory.CreateDirectory(_stateDirectory);
        _voiceService = new LocalVoiceExplanationService(_stateDirectory);
        LoadLearning();
        _folders = LoadOrBuildIndex();
        RenameTextBox.TextChanged += OnUserTextChanged;
        LearningEnabledCheckBox.Checked += OnLearningEnabledChanged;
        LearningEnabledCheckBox.Unchecked += OnLearningEnabledChanged;
        ResetLearningButton.Click += OnResetLearningClicked;
        _reminderTimer.Tick += OnReminderTick;
        _explorerPathTimer.Tick += OnExplorerPathTimerTick;
        _undoTimer.Tick += OnUndoTimerTick;
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
                _undoTimer.Stop();
                SaveLastMove(_pendingMove, "CLOSED_WITHOUT_CONFIRMATION");
                _pendingMove = null;
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
        AutoRenameCheckBox.IsChecked = false;
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
        _suggestions = await Task.Run(() => BuildSuggestions(_analysis));
        SuggestionList.ItemsSource = _suggestions;
        SuggestionList.SelectedIndex = _suggestions.Count > 0 ? 0 : -1;
        YesButton.IsEnabled = _suggestions.Count > 0;
        BuildFolderDecisionTree(_suggestions.FirstOrDefault()?.FullPath);

        if (_suggestions.Count == 0)
        {
            _proposedFolder = null;
            ProposedPathText.Text = "Aucun dossier assez fiable";
            ConfidenceText.Text = "Confiance inférieure à 30 % : clique sur OneDrive pour choisir manuellement.";
        }

        PrepareRename(fullPath, _analysis, _suggestions.FirstOrDefault());
        StatusText.Text = "Valide explicitement avant tout déplacement.";
    }

    private async void OnRefreshSuggestionClicked(object sender, RoutedEventArgs e)
    {
        if (_activePath is null || _pendingMove is not null || _moveInProgress) return;

        RefreshSuggestionButton.IsEnabled = false;
        StatusText.Text = "Actualisation des dossiers et nouvelle analyse…";
        try
        {
            _folders = await Task.Run(BuildIndex);
            SaveIndex(_folders);
            _analysis = await Task.Run(() => AnalyzeItemAsync(_activePath));
            _suggestions = await Task.Run(() => BuildSuggestions(_analysis));
            SuggestionList.ItemsSource = _suggestions;
            SuggestionList.SelectedIndex = _suggestions.Count > 0 ? 0 : -1;
            YesButton.IsEnabled = _suggestions.Count > 0;
            BuildFolderDecisionTree(_suggestions.FirstOrDefault()?.FullPath);

            if (_suggestions.Count == 0)
            {
                _proposedFolder = null;
                ProposedPathText.Text = "Aucun dossier assez fiable";
                ConfidenceText.Text = "Confiance inférieure à 30 % : clique sur OneDrive pour choisir manuellement.";
            }

            PrepareRename(_activePath, _analysis, _suggestions.FirstOrDefault());
            StatusText.Text = "Recherche relancée — vérifie la nouvelle proposition.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Impossible de relancer la recherche : " + ex.Message;
        }
        finally
        {
            RefreshSuggestionButton.IsEnabled = true;
        }
    }

    private async Task AnalyzeActiveFileAsync()
    {
        if (_activePath is null) return;
        var path = _activePath;
        _analysis = await Task.Run(() => AnalyzeItemAsync(path));
    }

    private async Task LoadAutomaticFolderCandidatesAsync()
    {
        if (_folders.Count == 0)
        {
            _folders = await Task.Run(() =>
                EnumerateFoldersToDepth(_options.OneDriveRoot, _options.MaxSuggestedDepth));
            SaveIndex(_folders);
        }
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
        ExpandBusinessTokens(tokens);
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

        var candidateFolders = GetRelevantFolderCandidates(analysis);
        var tokenDocumentFrequency = analysis.Tokens.ToDictionary(
            token => token,
            token => candidateFolders.Count(folder => folder.Tokens.Contains(token)),
            StringComparer.OrdinalIgnoreCase);

        var ranked = candidateFolders
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
                var thematicBoost = GetThematicBoost(analysis.Tokens, folder.Tokens);
                var distinctiveBoost = GetDistinctiveTokenBoost(
                    analysis.Tokens,
                    folder.Tokens,
                    tokenDocumentFrequency,
                    candidateFolders.Count);
                var hierarchyBoost = GetHierarchyBoost(relative, analysis.Tokens);
                var score = Math.Clamp(
                    scored.Score + learned * 0.03 + thematicBoost + distinctiveBoost + hierarchyBoost,
                    0d,
                    1d);
                var reason = thematicBoost > 0d
                    ? "correspondance thématique du dossier"
                    : distinctiveBoost > 0.04d
                        ? "termes distinctifs du dossier"
                        : hierarchyBoost > 0.04d
                            ? "branche et sous-dossier cohérents"
                            : scored.Reasons.FirstOrDefault() ?? "correspondance du dossier";
                return new SuggestionOption(folder.Path, score, reason);
            })
            .Where(x => x.Score >= 0.30)
            .GroupBy(x => x.FullPath, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.Score).First())
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.FullPath, StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToList();

        if (ranked.Count == 0) return ranked;

        var top = ranked[0];
        var runnerUpScore = ranked.Count > 1 ? ranked[1].Score : 0d;
        var calibratedConfidence = GetCalibratedConfidence(
            top.Score,
            runnerUpScore,
            analysis.Tokens.Count);
        if (calibratedConfidence < 0.30d) return new List<SuggestionOption>();

        return new List<SuggestionOption>
        {
            top with { Score = calibratedConfidence }
        };
    }

    private IReadOnlyList<FolderEntry> GetRelevantFolderCandidates(AnalysisSnapshot analysis)
    {
        if (_folders.Count <= 250)
            return _folders;

        var relevant = _folders
            .Where(folder =>
                folder.Depth == 1 ||
                folder.Tokens.Overlaps(analysis.Tokens) ||
                analysis.Years.Any(year => folder.Tokens.Contains(year.ToString())))
            .ToList();

        return relevant.Count >= 20 ? relevant : _folders;
    }

    private static double GetDistinctiveTokenBoost(
        IReadOnlySet<string> documentTokens,
        IReadOnlySet<string> folderTokens,
        IReadOnlyDictionary<string, int> tokenDocumentFrequency,
        int folderCount)
    {
        if (folderCount <= 0) return 0d;

        var boost = documentTokens
            .Where(folderTokens.Contains)
            .Sum(token =>
            {
                tokenDocumentFrequency.TryGetValue(token, out var frequency);
                var inverseFrequency = Math.Log((folderCount + 1d) / (frequency + 1d));
                return Math.Max(0d, inverseFrequency) * 0.025d;
            });

        return Math.Clamp(boost, 0d, 0.16d);
    }

    private static double GetHierarchyBoost(
        string relativePath,
        IReadOnlySet<string> documentTokens)
    {
        var parts = relativePath.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return 0d;

        var branchTokens = Tokenize(parts[0]);
        var descendantTokens = Tokenize(string.Join(' ', parts.Skip(1)));
        var branchMatches = branchTokens.Count(documentTokens.Contains);
        var descendantMatches = descendantTokens.Count(documentTokens.Contains);

        var branchBoost = Math.Min(0.06d, branchMatches * 0.03d);
        var descendantBoost = Math.Min(0.08d, descendantMatches * 0.025d);
        return branchBoost + descendantBoost;
    }

    private static double GetCalibratedConfidence(
        double topScore,
        double runnerUpScore,
        int evidenceTokenCount)
    {
        var margin = Math.Max(0d, topScore - runnerUpScore);
        var evidenceBonus = Math.Min(0.04d, evidenceTokenCount * 0.002d);
        var ambiguityPenalty = Math.Max(0d, 0.08d - margin) * 0.35d;
        return Math.Clamp(topScore + evidenceBonus - ambiguityPenalty, 0d, 1d);
    }

    private void OnSuggestionSelected(object sender, SelectionChangedEventArgs e)
    {
        if (SuggestionList.SelectedItem is not SuggestionOption option) return;
        _proposedFolder = option.FullPath;
        ProposedPathText.Text = ToOneDriveDisplayPath(option.FullPath);
        var confidenceLevel = option.Score >= 0.70d ? "ÉLEVÉE" : option.Score >= 0.45d ? "MOYENNE" : "FAIBLE";
        ConfidenceLevelText.Text = confidenceLevel;
        ConfidenceBadge.Background = confidenceLevel == "ÉLEVÉE"
            ? System.Windows.Media.Brushes.Honeydew
            : confidenceLevel == "MOYENNE"
                ? System.Windows.Media.Brushes.LemonChiffon
                : System.Windows.Media.Brushes.MistyRose;
        ConfidenceText.Text = $"Confiance {option.Score:P0} — {option.Reason}";
        CurrentMovePreviewText.Text = $"{Path.GetFileName(_activePath ?? string.Empty)}  →  {ToOneDriveDisplayPath(option.FullPath)}";
        YesButton.IsEnabled = true;
        if (_analysis is not null && _activePath is not null) PrepareRename(_activePath, _analysis, option);
    }

    private void BuildFolderDecisionTree(string? proposedFolder)
    {
        FolderTree.Items.Clear();
        var rootItem = NewTreeItem(_oneDriveRoot, "☁  OneDrive");
        rootItem.IsExpanded = true;
        FolderTree.Items.Add(rootItem);

        var branchPath = GetBranchPath(proposedFolder);
        var folders = string.IsNullOrWhiteSpace(branchPath)
            ? _folders.Where(folder => folder.Depth == 1).ToArray()
            : _folders.Where(folder => IsSameOrChild(folder.Path, branchPath)).ToArray();

        var nodes = new Dictionary<string, TreeViewItem>(StringComparer.OrdinalIgnoreCase)
        {
            [_oneDriveRoot] = rootItem
        };
        TreeViewItem? proposedNode = null;
        foreach (var folder in folders.OrderBy(folder => folder.Depth).ThenBy(folder => folder.Path, StringComparer.OrdinalIgnoreCase))
        {
            var parentPath = Path.GetDirectoryName(folder.Path) ?? _oneDriveRoot;
            if (!nodes.TryGetValue(parentPath, out var parent)) parent = rootItem;
            var node = NewTreeItem(folder.Path);
            node.IsExpanded = !string.IsNullOrWhiteSpace(proposedFolder) &&
                IsSameOrChild(proposedFolder, folder.Path);
            parent.Items.Add(node);
            nodes[folder.Path] = node;
            if (PathsEqualSafe(folder.Path, proposedFolder)) proposedNode = node;
        }

        if (proposedNode is not null)
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                () =>
                {
                    proposedNode.BringIntoView();
                    MainContentScrollViewer.ScrollToVerticalOffset(
                        Math.Max(0, MainContentScrollViewer.VerticalOffset - 60));
                });
        }
    }

    private string? GetBranchPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !IsUnderRoot(path)) return null;
        var relative = Path.GetRelativePath(_oneDriveRoot, path);
        var branch = relative.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(branch) ? null : Path.Combine(_oneDriveRoot, branch);
    }

    private static bool IsSameOrChild(string path, string parent)
    {
        try
        {
            var relative = Path.GetRelativePath(parent, path);
            return relative == "." || (!relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative));
        }
        catch { return false; }
    }

    private TreeViewItem NewTreeItem(string path, string? header = null)
    {
        var isProposed = PathsEqualSafe(path, _proposedFolder);
        var label = new TextBlock
        {
            Text = header ?? "📁  " + Path.GetFileName(path),
            Tag = path,
            Cursor = Cursors.Hand,
            Padding = new Thickness(5, 2, 7, 2),
            FontWeight = isProposed ? FontWeights.Bold : FontWeights.Normal,
            Foreground = isProposed
                ? System.Windows.Media.Brushes.DarkGreen
                : System.Windows.Media.Brushes.Black
        };
        label.MouseLeftButtonUp += OnFolderTreeNodeClicked;
        var highlight = new Border
        {
            Background = isProposed
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(236, 253, 243))
                : System.Windows.Media.Brushes.Transparent,
            BorderBrush = isProposed
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(74, 222, 128))
                : System.Windows.Media.Brushes.Transparent,
            BorderThickness = isProposed ? new Thickness(1) : new Thickness(0),
            CornerRadius = new CornerRadius(5),
            Child = label
        };
        return new TreeViewItem { Header = highlight, Tag = path };
    }

    private void OnFolderTreePreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled) return;
        MainContentScrollViewer.ScrollToVerticalOffset(
            MainContentScrollViewer.VerticalOffset - e.Delta);
        e.Handled = true;
    }

    private async void OnFolderTreeNodeClicked(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is not TextBlock { Tag: string destination } || _moveInProgress) return;

        if (PathsEqualSafe(destination, _oneDriveRoot))
        {
            _decisionPath = DecisionPath.WrongFolder;
            _initialSuggestedFolder = _proposedFolder;
            await EnterExplorerRefinementModeAsync(_oneDriveRoot);
            return;
        }

        var depth = GetDepth(_oneDriveRoot, destination);
        if (!Directory.Exists(destination) || !IsUnderRoot(destination) || depth is < 1 or > MaxDepth)
        {
            StatusText.Text = "Ce dossier n’est pas une destination autorisée.";
            return;
        }

        _decisionPath = PathsEqualSafe(GetBranchPath(destination), GetBranchPath(_proposedFolder))
            ? DecisionPath.GoodBranch
            : DecisionPath.WrongFolder;
        _initialSuggestedFolder = _proposedFolder;
        await ExecuteMoveOnceAsync(destination);
        await OpenDestinationBehindAsync(destination);
    }

    private async Task OpenDestinationBehindAsync(string destination)
    {
        var explorer = await OpenExplorerAndTrackAsync(destination);
        if (explorer is nint hwnd) ShowWindow(hwnd, ShowWindowMaximized);
        Dispatcher.Invoke(() => { Topmost = true; Activate(); });
    }

    private async void OnProposedPathClicked(object sender, MouseButtonEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_proposedFolder)) return;
        _decisionPath = DecisionPath.GoodBranch;
        _initialSuggestedFolder = _proposedFolder;
        await EnterExplorerRefinementModeAsync(_proposedFolder);
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
        _preparingRename = true;
        RenameTextBox.Text = suggestion.ProposedFileName;
        AutoRenameCheckBox.IsChecked = false;
        _preparingRename = false;
        AutoRenameStatusText.Text = suggestion.Changed
            ? "Proposition modifiable — coche la case uniquement si tu souhaites renommer."
            : "Nom actuel conservé : aucune amélioration fiable.";
    }

    private async void OnYes(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_proposedFolder)) return;
        _decisionPath = DecisionPath.Exact;
        _initialSuggestedFolder = _proposedFolder;
        await ExecuteMoveOnceAsync(_proposedFolder);
        _ = OpenDestinationBehindAsync(_proposedFolder);
    }

    private async Task EnterExplorerRefinementModeAsync(string startingFolder)
    {
        if (_activePath is null) return;

        _lockedSourcePath = _activePath;
        _trackedExplorerHwnd = null;
        SuggestionPanel.Visibility = Visibility.Collapsed;
        SelectedDestinationPanel.Visibility = Visibility.Collapsed;
        RenamePanel.Visibility = Visibility.Collapsed;
        MovePreviewPanel.Visibility = Visibility.Collapsed;
        ExplorerRefinementPanel.Visibility = Visibility.Visible;
        DecisionButtons.Visibility = Visibility.Collapsed;
        LearningControlsPanel.Visibility = Visibility.Collapsed;
        ExplainChoiceButton.Visibility = Visibility.Collapsed;
        PathLengthStatusText.Visibility = Visibility.Collapsed;
        LearningStatusText.Visibility = Visibility.Collapsed;
        _compactExplorerMode = true;
        PositionTopRight();
        BackButton.Visibility = Visibility.Visible;
        MoveHereButton.Content = "DÉPOSER DANS CE DOSSIER";
        MoveHereButton.IsEnabled = false;
        MoveHereButton.Visibility = Visibility.Visible;
        TrackedExplorerDestinationText.Text = ToOneDriveTreeDisplayPath(startingFolder);
        DestinationReadyText.Text = "● Ouverture de l’Explorateur…";
        DestinationReadyText.Foreground = System.Windows.Media.Brushes.DarkOrange;
        StatusText.Text = "Source verrouillée. Ouverture de l’Explorateur — aucun déplacement effectué.";

        _trackedExplorerHwnd = await OpenExplorerAndTrackAsync(startingFolder);
        if (_trackedExplorerHwnd is nint explorerHwnd)
        {
            ShowWindow(explorerHwnd, ShowWindowMaximized);
            Topmost = true;
            Activate();
            _explorerPathTimer.Start();
        }
        MoveHereButton.IsEnabled = _trackedExplorerHwnd is not null;
        StatusText.Text = _trackedExplorerHwnd is null
            ? "Explorateur introuvable : utilise RETOUR puis réessaie."
            : "Choisis le dossier dans l’Explorateur, puis clique DÉPOSER DANS CE DOSSIER.";
        if (_trackedExplorerHwnd is null)
        {
            DestinationReadyText.Text = "● Explorateur introuvable";
            DestinationReadyText.Foreground = System.Windows.Media.Brushes.Firebrick;
        }
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
        if (!Directory.Exists(destination) || !IsUnderRoot(destination) || depth < 0 || depth > MaxDepth)
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
        if (!_preparingRename && !string.IsNullOrWhiteSpace(RenameTextBox.Text))
        {
            AutoRenameStatusText.Text = AutoRenameCheckBox.IsChecked == true
                ? "✓ Ton nom personnalisé sera appliqué lors du déplacement."
                : "Proposition modifiée — coche la case pour appliquer ce renommage.";
        }
        _isUserTyping = true;
        _lastActivityUtc = DateTime.UtcNow;
        _reminderShown = false;
        _ = Task.Delay(900).ContinueWith(_ => Dispatcher.BeginInvoke(() => _isUserTyping = false));
    }

    private async void OnExplainChoiceClicked(object sender, RoutedEventArgs e)
    {
        VoiceRulePanel.Visibility = Visibility.Visible;
        VoiceTranscriptText.Text = string.Empty;
        VoiceRulePreviewText.Text = string.Empty;

        if (_pendingMove is null)
        {
            VoiceStatusText.Text = "Le classement doit d’abord être déplacé avant d’expliquer ton choix.";
            StatusText.Text = "Aucun déplacement en attente : le microphone n’a pas démarré.";
            return;
        }

        if (_voiceService is null)
        {
            VoiceStatusText.Text = "Module vocal indisponible. Relance Atlas Drop puis réessaie.";
            return;
        }

        if (!_voiceService.IsRecording)
        {
            VoiceStatusText.Text = "Activation du microphone…";
            ExplainChoiceButton.Content = "ACTIVATION…";
            ExplainChoiceButton.IsEnabled = false;
            await Dispatcher.Yield(DispatcherPriority.Render);
            try
            {
                _voiceService.StartRecording();
                ExplainChoiceButton.Content = "■ ARRÊTER ET ANALYSER";
                VoiceStatusText.Text = "🎤 Je t’écoute. Explique simplement pourquoi ce fichier va dans ce dossier.";
            }
            catch (Exception ex)
            {
                ExplainChoiceButton.Content = "🎤 EXPLIQUER MON CHOIX";
                VoiceStatusText.Text = "Microphone indisponible : vérifie l’autorisation Microphone de Windows. " + ex.Message;
            }
            finally
            {
                ExplainChoiceButton.IsEnabled = true;
            }
            return;
        }

        ExplainChoiceButton.IsEnabled = false;
        ExplainChoiceButton.Content = "ANALYSE EN COURS…";
        try
        {
            var progress = new Progress<string>(message => VoiceStatusText.Text = message);
            var transcription = await _voiceService.StopAndTranscribeAsync(progress);
            if (string.IsNullOrWhiteSpace(transcription))
            {
                VoiceStatusText.Text = "Aucune phrase comprise. Clique sur le micro pour réessayer.";
                return;
            }

            _pendingVoiceExplanation = transcription;
            var tokens = ExtractVoiceLearningTokens(transcription);
            VoiceStatusText.Text = "Voici ce qu’Atlas Drop a compris. Confirme avant tout apprentissage.";
            VoiceTranscriptText.Text = $"« {transcription} »";
            VoiceRulePreviewText.Text = tokens.Length == 0
                ? "Aucun mot suffisamment précis détecté."
                : $"Règle proposée : {string.Join(", ", tokens)}  →  {ToOneDriveDisplayPath(_pendingMove.Destination)}";
        }
        catch (Exception ex)
        {
            VoiceStatusText.Text = "Analyse vocale impossible : " + ex.Message + " Tu peux réessayer sans modifier le classement.";
        }
        finally
        {
            ExplainChoiceButton.IsEnabled = true;
            ExplainChoiceButton.Content = "🎤 EXPLIQUER MON CHOIX";
        }
    }

    private void OnConfirmVoiceRuleClicked(object sender, RoutedEventArgs e)
    {
        if (_pendingMove is null || string.IsNullOrWhiteSpace(_pendingVoiceExplanation)) return;

        var tokens = ExtractVoiceLearningTokens(_pendingVoiceExplanation);
        if (tokens.Length == 0)
        {
            VoiceStatusText.Text = "Règle non enregistrée : aucun mot suffisamment précis.";
            return;
        }

        foreach (var token in tokens)
        {
            var key = LearningKey(token, _pendingMove.Destination);
            _learning.TryGetValue(key, out var score);
            _learning[key] = Math.Clamp(score + StrongPositiveLearningWeight, -20, 50);
        }

        SaveLearningDictionary();
        VoiceStatusText.Text = "✓ Explication enregistrée dans l’apprentissage.";
        VoiceRulePreviewText.Text = string.Empty;
        _pendingVoiceExplanation = null;
    }

    private void OnCancelVoiceRuleClicked(object sender, RoutedEventArgs e)
    {
        _pendingVoiceExplanation = null;
        VoiceRulePanel.Visibility = Visibility.Collapsed;
        VoiceTranscriptText.Text = string.Empty;
        VoiceRulePreviewText.Text = string.Empty;
        VoiceStatusText.Text = "Explication ignorée. Aucun apprentissage ajouté.";
    }

    private static string[] ExtractVoiceLearningTokens(string explanation)
    {
        var tokens = Tokenize(explanation);
        ExpandBusinessTokens(tokens);
        var stopWords = new HashSet<string>(
            new[]
            {
                "dans", "pour", "avec", "parce", "cette", "fichier", "dossier",
                "mettre", "rangé", "range", "cela", "celui", "donc", "ici", "chez",
                "sont", "est", "une", "des", "les", "mon", "mes", "sur"
            },
            StringComparer.OrdinalIgnoreCase);
        return tokens
            .Where(token => !stopWords.Contains(token))
            .OrderByDescending(token => token.Length)
            .ThenBy(token => token, StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToArray();
    }

    private void OnExplorerPathTimerTick(object? sender, EventArgs e)
    {
        if (_trackedExplorerHwnd is not nint explorerHwnd ||
            !TryGetExplorerPathByHwnd(explorerHwnd, out var destination))
        {
            MoveHereButton.IsEnabled = false;
            DestinationReadyText.Text = "● Explorateur fermé ou inaccessible";
            DestinationReadyText.Foreground = System.Windows.Media.Brushes.Firebrick;
            return;
        }

        var depth = GetDepth(_oneDriveRoot, destination);
        var valid = Directory.Exists(destination) && IsUnderRoot(destination) && depth is >= 0 and <= MaxDepth;
        TrackedExplorerDestinationText.Text = ToOneDriveTreeDisplayPath(destination);
        MoveHereButton.IsEnabled = valid;
        DestinationReadyText.Text = valid ? "● Destination prête" : "● Choisis un dossier OneDrive de niveau 0 à 4";
        DestinationReadyText.Foreground = valid ? System.Windows.Media.Brushes.ForestGreen : System.Windows.Media.Brushes.DarkOrange;
    }

    private void OnUndoTimerTick(object? sender, EventArgs e)
    {
        _undoSecondsRemaining--;
        if (_undoSecondsRemaining <= 0)
        {
            _undoTimer.Stop();
            UndoMoveButton.IsEnabled = false;
            UndoMoveButton.Visibility = Visibility.Collapsed;
            return;
        }

        UndoMoveButton.Content = $"ANNULER LE DÉPLACEMENT ({_undoSecondsRemaining} s)";
    }

    private async void OnBack(object sender, RoutedEventArgs e)
    {
        if (_pendingMove is not null)
        {
            _undoTimer.Stop();
            var move = _pendingMove;
            IsEnabled = false;
            StatusText.Text = "Annulation du déplacement…";
            try
            {
                var restored = await Task.Run(() => RestoreMove(move));
                _pendingMove = null;
                _activePath = restored;
                SaveLastMove(move, "BACK_AND_RESTORED");
                ItemNameText.Text = Path.GetFileName(restored);
                PostMovePanel.Visibility = Visibility.Collapsed;
                ExplainChoiceButton.IsEnabled = false;
                ExplainChoiceButton.Content = "🎤 EXPLICATION VOCALE — DISPONIBLE APRÈS CLASSEMENT";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Retour impossible : " + ex.Message;
                IsEnabled = true;
                return;
            }
            finally
            {
                IsEnabled = true;
            }
        }

        _explorerPathTimer.Stop();
        _trackedExplorerHwnd = null;
        _lockedSourcePath = null;
        ExplorerRefinementPanel.Visibility = Visibility.Collapsed;
        MoveHereButton.Visibility = Visibility.Collapsed;
        SuggestionPanel.Visibility = Visibility.Visible;
        SelectedDestinationPanel.Visibility = Visibility.Visible;
        RenamePanel.Visibility = Directory.Exists(_activePath ?? string.Empty) ? Visibility.Collapsed : Visibility.Visible;
        MovePreviewPanel.Visibility = Visibility.Visible;
        DecisionButtons.Visibility = Visibility.Visible;
        LearningControlsPanel.Visibility = Visibility.Visible;
        ExplainChoiceButton.Visibility = Visibility.Visible;
        PathLengthStatusText.Visibility = Visibility.Visible;
        LearningStatusText.Visibility = Visibility.Visible;
        _compactExplorerMode = false;
        PositionTopRight();
        BackButton.Visibility = Visibility.Collapsed;
        StatusText.Text = "Retour à la proposition. Déplacement annulé.";
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (ExplorerRefinementPanel.Visibility == Visibility.Visible) OnBack(sender, e);
            else OnCancel(sender, e);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && MoveHereButton.Visibility == Visibility.Visible && MoveHereButton.IsEnabled)
        {
            OnMoveHere(sender, e);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && DecisionButtons.Visibility == Visibility.Visible && YesButton.IsEnabled)
        {
            OnYes(sender, e);
            e.Handled = true;
        }
    }

    private void OnLearningEnabledChanged(object sender, RoutedEventArgs e)
    {
        _learningSettings.SetEnabled(LearningEnabledCheckBox.IsChecked == true);
        LearningStatusText.Text = _learningSettings.IsEnabled ? "Apprentissage actif" : "Apprentissage désactivé";
    }

    private async void OnResetLearningClicked(object sender, RoutedEventArgs e)
    {
        var confirmation = MessageBox.Show(
            "Effacer tout l’apprentissage enregistré depuis le début ?\n\nAucun fichier OneDrive ne sera supprimé.",
            "Tout effacer",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (confirmation != MessageBoxResult.Yes) return;

        await _learningService.ClearAsync();
        _learning.Clear();
        try { File.Delete(Path.Combine(_stateDirectory, "learning-v108.json")); } catch { }
        LearningStatusText.Text = "Tout l’apprentissage a été effacé.";
    }

    private void OnManageLearningClicked(object sender, RoutedEventArgs e)
    {
        var window = new Window
        {
            Title = "Gérer l’apprentissage Atlas Drop",
            Owner = this,
            Width = 620,
            Height = 520,
            MinWidth = 520,
            MinHeight = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = System.Windows.Media.Brushes.White
        };

        var root = new DockPanel { Margin = new Thickness(14) };
        var title = new TextBlock
        {
            Text = "Coche les apprentissages à supprimer",
            FontSize = 19,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 8)
        };
        DockPanel.SetDock(title, Dock.Top);
        root.Children.Add(title);

        var help = new TextBlock
        {
            Text = "Les autres apprentissages seront conservés. Aucun fichier OneDrive ne sera touché.",
            Foreground = System.Windows.Media.Brushes.DimGray,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 10)
        };
        DockPanel.SetDock(help, Dock.Top);
        root.Children.Add(help);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 10, 0, 0)
        };
        DockPanel.SetDock(actions, Dock.Bottom);

        var deleteSelected = new Button
        {
            Content = "SUPPRIMER LA SÉLECTION",
            Background = System.Windows.Media.Brushes.Firebrick,
            Foreground = System.Windows.Media.Brushes.White
        };
        var close = new Button { Content = "FERMER" };
        actions.Children.Add(deleteSelected);
        actions.Children.Add(close);
        root.Children.Add(actions);

        var list = new StackPanel();
        var groups = _learning
            .GroupBy(entry => LearningFolderFromKey(entry.Key), StringComparer.OrdinalIgnoreCase)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (groups.Length == 0)
        {
            list.Children.Add(new TextBlock
            {
                Text = "Aucun apprentissage enregistré.",
                Foreground = System.Windows.Media.Brushes.DimGray,
                Margin = new Thickness(4)
            });
        }
        else
        {
            foreach (var group in groups)
            {
                var tokens = group
                    .Select(entry => LearningTokenFromKey(entry.Key))
                    .Where(token => !string.IsNullOrWhiteSpace(token))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(token => token, StringComparer.OrdinalIgnoreCase)
                    .Take(12);
                list.Children.Add(new CheckBox
                {
                    Content = $"{ToOneDriveDisplayPath(group.Key)}\nMots : {string.Join(", ", tokens)}",
                    Tag = group.Select(entry => entry.Key).ToArray(),
                    Margin = new Thickness(3, 5, 3, 5),
                    Padding = new Thickness(5),
                    FontWeight = FontWeights.SemiBold
                });
            }
        }

        var scroll = new ScrollViewer
        {
            Content = list,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        root.Children.Add(scroll);
        window.Content = root;

        deleteSelected.Click += (_, _) =>
        {
            var selectedKeys = list.Children
                .OfType<CheckBox>()
                .Where(checkBox => checkBox.IsChecked == true)
                .SelectMany(checkBox => (string[])(checkBox.Tag ?? Array.Empty<string>()))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (selectedKeys.Length == 0)
            {
                MessageBox.Show("Coche au moins un apprentissage.", "Atlas Drop", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (var key in selectedKeys) _learning.Remove(key);
            SaveLearningDictionary();
            LearningStatusText.Text = $"{selectedKeys.Length} apprentissage(s) supprimé(s).";
            window.Close();
        };
        close.Click += (_, _) => window.Close();
        window.ShowDialog();
    }

    private void OnHistoryClicked(object sender, RoutedEventArgs e)
    {
        var history = LoadMoveHistory();
        if (history.Count == 0)
        {
            MessageBox.Show("Aucun classement dans l’historique.", "Historique Atlas Drop", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var lines = history.Select(entry =>
            $"{entry.TimestampUtc.ToLocalTime():dd/MM HH:mm}  •  {Path.GetFileName(entry.Target)}\n→ {ToOneDriveDisplayPath(entry.Destination)}");
        MessageBox.Show(
            string.Join("\n\n", lines),
            "10 derniers classements",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void SaveLearningDictionary()
    {
        try
        {
            File.WriteAllText(
                Path.Combine(_stateDirectory, "learning-v108.json"),
                JsonSerializer.Serialize(_learning));
        }
        catch
        {
            LearningStatusText.Text = "Impossible d’enregistrer la modification de l’apprentissage.";
        }
    }

    private static string LearningFolderFromKey(string key)
    {
        var separator = key.IndexOf("=>", StringComparison.Ordinal);
        return separator >= 0 ? key[(separator + 2)..] : string.Empty;
    }

    private static string LearningTokenFromKey(string key)
    {
        var separator = key.IndexOf("=>", StringComparison.Ordinal);
        return separator > 0 ? key[..separator] : string.Empty;
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
        if (!IsUnderRoot(destination) || !Directory.Exists(destination) || destinationDepth < 0 || destinationDepth > MaxDepth)
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
            SelectedDestinationPanel.Visibility = Visibility.Visible;
            ProposedPathText.Text = ToOneDriveDisplayPath(destination);
            ExplainChoiceButton.Visibility = Visibility.Visible;
            PathLengthStatusText.Visibility = Visibility.Visible;
            LearningStatusText.Visibility = Visibility.Visible;
            PostMovePanel.Visibility = Visibility.Visible;
            PostMovePanel.BringIntoView();
            MainContentScrollViewer.ScrollToEnd();
            ExplainChoiceButton.IsEnabled = true;
            ExplainChoiceButton.Content = "🎤 EXPLIQUER MON CHOIX";
            LearningControlsPanel.Visibility = Visibility.Visible;
            _compactExplorerMode = false;
            PositionTopRight();
            BackButton.Visibility = Visibility.Visible;
            _explorerPathTimer.Stop();
            _undoSecondsRemaining = 10;
            UndoMoveButton.Content = "ANNULER LE DÉPLACEMENT (10 s)";
            UndoMoveButton.Visibility = Visibility.Visible;
            UndoMoveButton.IsEnabled = true;
            _undoTimer.Start();
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
        _undoTimer.Stop();
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
        _undoTimer.Stop();
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

        foreach (var ancestor in GetLearningAncestors(finalDestination))
        {
            if (!signals.ContainsKey(ancestor))
                signals[ancestor] = WeakPositiveLearningWeight;
        }

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

    private IEnumerable<string> GetLearningAncestors(string destination)
    {
        if (!IsUnderRoot(destination)) yield break;

        var current = Directory.GetParent(destination);
        while (current is not null && IsUnderRoot(current.FullName) && !SamePath(current.FullName, _oneDriveRoot))
        {
            yield return current.FullName;
            current = current.Parent;
        }
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
            var timestamp = DateTime.UtcNow;
            var audit = new { move.Source, move.Target, move.Destination, Status = status, TimestampUtc = timestamp };
            File.WriteAllText(Path.Combine(_stateDirectory, "last-move-v108.json"), JsonSerializer.Serialize(audit));

            if (!string.Equals(status, "PENDING_CONFIRMATION", StringComparison.OrdinalIgnoreCase))
            {
                var history = LoadMoveHistory();
                history.Insert(0, new MoveHistoryEntry(move.Source, move.Target, move.Destination, status, timestamp));
                File.WriteAllText(
                    Path.Combine(_stateDirectory, "move-history-v110.json"),
                    JsonSerializer.Serialize(history.Take(10).ToList()));
            }
        }
        catch { }
    }

    private List<MoveHistoryEntry> LoadMoveHistory()
    {
        try
        {
            var path = Path.Combine(_stateDirectory, "move-history-v110.json");
            if (!File.Exists(path)) return new List<MoveHistoryEntry>();
            return JsonSerializer.Deserialize<List<MoveHistoryEntry>>(File.ReadAllText(path))
                ?.OrderByDescending(entry => entry.TimestampUtc)
                .Take(10)
                .ToList() ?? new List<MoveHistoryEntry>();
        }
        catch
        {
            return new List<MoveHistoryEntry>();
        }
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

    private static double GetThematicBoost(
        IReadOnlySet<string> documentTokens,
        IReadOnlySet<string> folderTokens)
    {
        var health = documentTokens.Contains("santé") || documentTokens.Contains("sante");
        var healthFolder = folderTokens.Contains("santé") || folderTokens.Contains("sante");
        var fiscal = documentTokens.Overlaps(new[] { "impot", "impôts", "impots", "fiscal", "fiscale", "revenu", "revenus" });
        var fiscalFolder = folderTokens.Overlaps(new[] { "impot", "impôts", "impots", "fiscal", "finances" });
        if (fiscal && fiscalFolder) return 0.32d;
        return health && healthFolder ? 0.25d : 0d;
    }

    private static void ExpandBusinessTokens(HashSet<string> tokens)
    {
        var healthTerms = new HashSet<string>(
            new[]
            {
                "ordonnance", "biologie", "laboratoire", "analyse", "analyses",
                "medical", "médical", "medecin", "médecin", "sante", "santé"
            },
            StringComparer.OrdinalIgnoreCase);
        if (tokens.Overlaps(healthTerms))
        {
            tokens.Add("santé");
            tokens.Add("sante");
            tokens.Add("médical");
            tokens.Add("medical");
        }

        var fiscalTerms = new HashSet<string>(
            new[] { "impot", "impôts", "impots", "fiscal", "fiscale", "fiscaux", "revenu", "revenus", "imposition" },
            StringComparer.OrdinalIgnoreCase);
        if (tokens.Overlaps(fiscalTerms))
        {
            tokens.Add("impôt");
            tokens.Add("impot");
            tokens.Add("impôts");
            tokens.Add("impots");
            tokens.Add("fiscal");
            tokens.Add("finances");
        }
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
        ShowWindow(hwnd, ShowWindowRestore);
        MoveWindow(
            hwnd,
            area.Left,
            area.Top,
            area.Right - area.Left,
            area.Bottom - area.Top,
            true);
        ShowWindow(hwnd, ShowWindowMaximized);
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
        var preferredRightGapDip = workWidthDip >= MinWidth + 174 ? 150d : 12d;
        var maxWidthDip = Math.Max(MinWidth, workWidthDip - preferredRightGapDip - 24);
        var maxHeightDip = Math.Max(MinHeight, workHeightDip - 28);

        Width = Math.Min(maxWidthDip, Math.Max(520, workWidthDip * .323));
        Height = _compactExplorerMode
            ? Math.Min(maxHeightDip, 430)
            : Math.Min(maxHeightDip, Math.Max(680, workHeightDip * .94));

        var widthPixels = (int)Math.Round(Width * monitor.ScaleX);
        var heightPixels = (int)Math.Round(Height * monitor.ScaleY);
        var rightGapPixels = (int)Math.Round(preferredRightGapDip * monitor.ScaleX);
        var leftPixels = Math.Max(
            area.Left + (int)Math.Round(12 * monitor.ScaleX),
            area.Right - rightGapPixels - widthPixels);
        var topPixels = area.Top + (int)Math.Round(16 * monitor.ScaleY);

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

    private string ToOneDriveTreeDisplayPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !IsUnderRoot(path))
            return Path.GetFileName(path);

        var relative = Path.GetRelativePath(_oneDriveRoot, path);
        if (string.IsNullOrWhiteSpace(relative) || relative == ".")
            return "📁 OneDrive";

        var segments = relative.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string> { "📁 OneDrive" };
        for (var index = 0; index < segments.Length; index++)
        {
            var prefix = new string('　', index + 1) + (index == segments.Length - 1 ? "└ 🎯 " : "└ 📁 ");
            lines.Add(prefix + segments[index]);
        }
        return string.Join(Environment.NewLine, lines);
    }

    private void ResetOperation()
    {
        _closeTimer.Stop();
        _explorerPathTimer.Stop();
        _undoTimer.Stop();
        _explorerOpenRequestId++;
        _activePath = null; _proposedFolder = null; _analysis = null; _pendingMove = null; _busy = false;
        _decisionPath = DecisionPath.None; _initialSuggestedFolder = null; _lockedSourcePath = null; _trackedExplorerHwnd = null;
        _rejectedDestinations.Clear(); _learningCommitted = false; _moveInProgress = false;
        _pendingVoiceExplanation = null;
        _compactExplorerMode = false;
        VoiceRulePanel.Visibility = Visibility.Collapsed;
        ExplainChoiceButton.Content = "🎤 EXPLICATION VOCALE — DISPONIBLE APRÈS CLASSEMENT";
        ExplainChoiceButton.IsEnabled = false;
        _suggestions.Clear(); SuggestionList.ItemsSource = null; FolderTree.Items.Clear();
        ItemNameText.Text = "En attente d’un clic molette…"; ProposedPathText.Text = "—"; ConfidenceText.Text = "";
        TrackedExplorerDestinationText.Text = "Ouverture de l’Explorateur…";
        RenameTextBox.Text = ""; AutoRenameStatusText.Text = ""; AutoRenameCheckBox.IsChecked = false;
        ExplorerRefinementPanel.Visibility = Visibility.Collapsed; PostMovePanel.Visibility = Visibility.Collapsed;
        SuggestionPanel.Visibility = Visibility.Visible; SelectedDestinationPanel.Visibility = Visibility.Visible; MovePreviewPanel.Visibility = Visibility.Visible;
        LearningControlsPanel.Visibility = Visibility.Visible;
        ExplainChoiceButton.Visibility = Visibility.Visible;
        PathLengthStatusText.Visibility = Visibility.Visible;
        LearningStatusText.Visibility = Visibility.Visible;
        BackButton.Visibility = Visibility.Collapsed;
        DecisionButtons.Visibility = Visibility.Visible; MoveHereButton.Visibility = Visibility.Collapsed; RenamePanel.Visibility = Visibility.Visible;
        MoveHereButton.IsEnabled = false; YesButton.IsEnabled = false; IsEnabled = true;
        PositionTopRight();
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
    private sealed record MoveHistoryEntry(string Source, string Target, string Destination, string Status, DateTime TimestampUtc);
    private sealed record ExplorerWindowSnapshot(nint Hwnd, string? Path);
    private sealed record MonitorPlacement(NativeRect WorkArea, double ScaleX, double ScaleY);
    private enum DecisionPath { None, Exact, GoodBranch, WrongFolder }

    private const uint MonitorDefaultToNearest = 2;
    private const int MonitorDpiTypeEffective = 0;
    private const int ShowWindowMaximized = 3;
    private const int ShowWindowRestore = 9;

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

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint window, int command);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MoveWindow(IntPtr window, int x, int y, int width, int height, bool repaint);
}
