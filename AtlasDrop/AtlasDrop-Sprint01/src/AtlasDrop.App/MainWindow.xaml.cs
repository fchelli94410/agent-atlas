using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using AtlasDrop.Analysis.Office;
using AtlasDrop.Analysis.Pdf;
using AtlasDrop.Analysis.Text;
using AtlasDrop.Core.Analysis;
using AtlasDrop.Infrastructure.Notifications;

namespace AtlasDrop.App;

public partial class MainWindow : Window
{
    private const int MaxDepth = 5;
    private readonly WindowsUserFeedbackService _feedback = new();
    private readonly DispatcherTimer _closeTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly DispatcherTimer _indexRefreshTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private readonly string _oneDriveRoot;
    private readonly string _stateDirectory;
    private List<FolderEntry> _folders = new();
    private Dictionary<string, int> _learning = new(StringComparer.OrdinalIgnoreCase);
    private string? _activePath;
    private string? _proposedFolder;
    private string? _manualFolder;
    private bool _busy;
    private FileSystemWatcher? _indexWatcher;

    public MainWindow()
    {
        InitializeComponent();
        _oneDriveRoot = FindOneDriveRoot();
        _stateDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AtlasDrop");
        Directory.CreateDirectory(_stateDirectory);
        LoadLearning();
        _folders = LoadOrBuildIndex();
        _closeTimer.Tick += (_, _) => { _closeTimer.Stop(); Hide(); ResetOperation(); };
        _indexRefreshTimer.Tick += async (_, _) =>
        {
            _indexRefreshTimer.Stop();
            _folders = await Task.Run(BuildIndex);
            SaveIndex(_folders);
        };
        StartIndexWatcher();
        Loaded += (_, _) => PositionTopRight();
        Closing += (_, e) => { e.Cancel = true; Hide(); ResetOperation(); };
    }

    public void SignalMiddleClickDetected()
    {
        StatusText.Text = "Clic détecté — analyse en cours…";
    }

    public async void ActivateFile(string path)
    {
        if (_busy || string.IsNullOrWhiteSpace(path)) return;
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath) && !Directory.Exists(fullPath)) return;

        _busy = true;
        _activePath = fullPath;
        ItemNameText.Text = Path.GetFileName(fullPath);
        ManualPanel.Visibility = Visibility.Collapsed;
        DecisionButtons.Visibility = Visibility.Visible;
        MoveHereButton.Visibility = Visibility.Collapsed;
        StatusText.Visibility = Visibility.Visible;
        StatusText.Text = "Analyse en cours…";
        Show(); Activate(); PositionTopRight();

        var tokens = await AnalyzeItemAsync(fullPath);
        _proposedFolder = ChooseSafeDestination(tokens);
        ProposedPathText.Text = _proposedFolder;
        ConfidenceText.Text = BuildConfidenceText(tokens, _proposedFolder);
        StatusText.Text = "Valide le dossier avant tout déplacement.";
        OpenExplorer(_proposedFolder);
    }

    private async Task<IReadOnlyCollection<string>> AnalyzeItemAsync(string path)
    {
        var text = new List<string> { Path.GetFileNameWithoutExtension(path) };
        if (Directory.Exists(path))
        {
            try
            {
                text.AddRange(Directory.EnumerateFileSystemEntries(path)
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
                if (!string.IsNullOrWhiteSpace(result?.Text)) text.Add(result.Text[..Math.Min(result.Text.Length, 100_000)]);
            }
            catch { }
        }
        return Tokenize(string.Join(' ', text));
    }

    private string ChooseSafeDestination(IReadOnlyCollection<string> tokens)
    {
        if (_folders.Count == 0) return _oneDriveRoot;
        var ranked = _folders.Select(f =>
        {
            var overlap = f.Tokens.Count(t => tokens.Contains(t));
            var learned = tokens.Sum(token => _learning.TryGetValue(LearningKey(token, f.Path), out var value) ? value : 0);
            return new { Folder = f, Score = overlap * 10 + learned * 6 + Math.Min(f.Depth, 3) };
        }).OrderByDescending(x => x.Score).ThenByDescending(x => x.Folder.Depth).ToArray();

        var best = ranked[0];
        if (best.Score < 10) return _oneDriveRoot;
        if (ranked.Length > 1 && best.Score - ranked[1].Score < 7)
            return Directory.GetParent(best.Folder.Path)?.FullName ?? _oneDriveRoot;
        return best.Folder.Path;
    }

    private string BuildConfidenceText(IReadOnlyCollection<string> tokens, string folder)
    {
        var entry = _folders.FirstOrDefault(x => string.Equals(x.Path, folder, StringComparison.OrdinalIgnoreCase));
        var count = entry?.Tokens.Count(tokens.Contains) ?? 0;
        return folder == _oneDriveRoot ? "Confiance faible : racine OneDrive proposée." :
            count >= 2 ? "Confiance élevée" : "Confiance prudente : dossier parent sûr";
    }

    private async void OnYes(object sender, RoutedEventArgs e) => await MoveAsync(_proposedFolder, true);

    private void OnNo(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_proposedFolder)) return;
        RecordLearning(_proposedFolder, accepted: false);
        var parent = Directory.GetParent(_proposedFolder)?.FullName;
        _proposedFolder = !string.IsNullOrWhiteSpace(parent) && IsUnderRoot(parent) ? parent : _oneDriveRoot;
        ProposedPathText.Text = _proposedFolder;
        ConfidenceText.Text = "Proposition remontée d’un niveau.";
        OpenExplorer(_proposedFolder);
    }

    private void OnChoose(object sender, RoutedEventArgs e)
    {
        BuildFolderTree();
        ManualPanel.Visibility = Visibility.Visible;
        DecisionButtons.Visibility = Visibility.Collapsed;
        StatusText.Visibility = Visibility.Collapsed;
        MoveHereButton.Visibility = Visibility.Visible;
        Height = 650;
    }

    private void OnFolderSelected(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not TreeViewItem item || item.Tag is not string path) return;
        _manualFolder = path;
        ProposedPathText.Text = path;
        OpenExplorer(path);
    }

    private async void OnMoveHere(object sender, RoutedEventArgs e) => await MoveAsync(_manualFolder, true);

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        Hide(); ResetOperation();
    }

    private async Task MoveAsync(string? destination, bool learn)
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
            var requestedTarget = Path.Combine(destination, Path.GetFileName(source));
            var overwrite = false;
            string target;
            if (!sourceIsDirectory && File.Exists(requestedTarget))
            {
                var choice = MessageBox.Show(
                    "Un fichier du même nom existe déjà.\n\nOUI = remplacer\nNON = renommer automatiquement (recommandé)\nANNULER = ne rien déplacer",
                    "Doublon détecté", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning, MessageBoxResult.No);
                if (choice == MessageBoxResult.Cancel) return;
                overwrite = choice == MessageBoxResult.Yes;
                target = overwrite ? requestedTarget : AvailableTarget(destination, Path.GetFileName(source), false);
            }
            else target = AvailableTarget(destination, Path.GetFileName(source), sourceIsDirectory);
            var originalCreation = File.Exists(source) ? File.GetCreationTimeUtc(source) : DateTime.MinValue;
            var originalWrite = File.Exists(source) ? File.GetLastWriteTimeUtc(source) : DateTime.MinValue;
            StatusText.Visibility = Visibility.Visible;
            StatusText.Text = "Déplacement sécurisé…";
            IsEnabled = false;
            await Task.Run(() =>
            {
                if (Directory.Exists(source)) Directory.Move(source, target);
                else File.Move(source, target, overwrite);
            });
            if (!File.Exists(target) && !Directory.Exists(target)) throw new IOException("Vérification du déplacement impossible.");
            if (File.Exists(target)) { File.SetCreationTimeUtc(target, originalCreation); File.SetLastWriteTimeUtc(target, originalWrite); }
            if (learn) RecordLearning(destination, accepted: true);
            _feedback.PlaySuccessSound();
            StatusText.Text = "Classement réussi";
            IsEnabled = true;
            if (File.Exists(target)) _closeTimer.Start();
            else { _activePath = null; _busy = false; OpenExplorer(target); }
        }
        catch (Exception ex)
        {
            IsEnabled = true;
            StatusText.Text = "Déplacement impossible : " + ex.Message;
        }
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

    private static TreeViewItem NewTreeItem(string path) => new() { Header = Path.GetFileName(path), Tag = path };

    private List<FolderEntry> LoadOrBuildIndex()
    {
        var cache = Path.Combine(_stateDirectory, "folder-index-v107.json");
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
        try { File.WriteAllText(Path.Combine(_stateDirectory, "folder-index-v107.json"), JsonSerializer.Serialize(folders)); } catch { }
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
            var file = Path.Combine(_stateDirectory, "learning-v107.json");
            if (File.Exists(file)) _learning = JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(file)) ?? _learning;
        }
        catch { }
    }

    private void RecordLearning(string folder, bool accepted)
    {
        if (_activePath is null) return;
        foreach (var token in Tokenize(Path.GetFileNameWithoutExtension(_activePath)).Take(12))
        {
            var key = LearningKey(token, folder);
            _learning.TryGetValue(key, out var score); _learning[key] = Math.Clamp(score + (accepted ? 1 : -1), -5, 20);
        }
        try { File.WriteAllText(Path.Combine(_stateDirectory, "learning-v107.json"), JsonSerializer.Serialize(_learning)); } catch { }
    }

    private static string LearningKey(string token, string folder) => token.ToLowerInvariant() + "=>" + folder.ToLowerInvariant();
    private static HashSet<string> Tokenize(string text) => text.ToLowerInvariant().Split(new[] { ' ', '\\', '/', '-', '_', '.', '(', ')', '[', ']', '&', ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Where(x => x.Length >= 3).ToHashSet(StringComparer.OrdinalIgnoreCase);

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
                    MoveWindow((IntPtr)(long)window.HWND, (int)(area.Left + area.Width * .73), (int)area.Top + 8,
                        (int)(area.Width * .26), (int)(area.Height * .50), true);
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
        Width = Math.Max(460, area.Width * .27); Height = Math.Max(390, area.Height * .46);
        Left = area.Right - Width - 12; Top = area.Bottom - Height - 12;
    }

    private void ResetOperation()
    {
        _activePath = null; _proposedFolder = null; _manualFolder = null; _busy = false; Height = 430;
        ItemNameText.Text = "En attente d’un clic molette…"; ProposedPathText.Text = "—"; ConfidenceText.Text = "";
    }

    public sealed record FolderEntry(string Path, int Depth, HashSet<string> Tokens);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MoveWindow(IntPtr window, int x, int y, int width, int height, bool repaint);
}
