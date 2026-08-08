using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using Microsoft.Win32;

namespace AtlasDrop.App;

/// <summary>
/// Captures a middle click only when it targets an existing item displayed by
/// Windows Explorer. Clicks made elsewhere are never intercepted.
/// </summary>
internal sealed class ExplorerMiddleClickActivation : IDisposable
{
    private const int WhMouseLl = 14;
    private const int WmMButtonDown = 0x0207;

    private readonly LowLevelMouseProc _callback;
    private IntPtr _hook;
    private int _resolutionInProgress;

    public ExplorerMiddleClickActivation()
    {
        _callback = OnMouseEvent;
    }

    public event EventHandler<string>? ItemActivated;
    public event EventHandler? ExplorerClickDetected;
    public event EventHandler<string>? ItemResolutionFailed;

    public void Start()
    {
        if (_hook != IntPtr.Zero || !OperatingSystem.IsWindows())
            return;

        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule;
        var moduleHandle = GetModuleHandle(module?.ModuleName);

        _hook = SetWindowsHookEx(
            WhMouseLl,
            _callback,
            moduleHandle,
            0);

        if (_hook == IntPtr.Zero)
            throw new InvalidOperationException(
                "Impossible d'activer le clic molette Atlas Drop.");
    }

    public void Dispose()
    {
        if (_hook == IntPtr.Zero)
            return;

        UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    private IntPtr OnMouseEvent(
        int code,
        IntPtr message,
        IntPtr data)
    {
        if (code >= 0 && message == (IntPtr)WmMButtonDown)
        {
            var mouse = Marshal.PtrToStructure<MsllHookStruct>(data);
            var rootWindow = GetAncestor(
                WindowFromPoint(mouse.Point),
                GetAncestorFlags.Root);

            if (rootWindow != IntPtr.Zero && IsSupportedShellWindow(rootWindow))
            {
                ExplorerClickDetected?.Invoke(this, EventArgs.Empty);

                // UI Automation and Shell COM can be slow. Never perform them
                // inside the global mouse-hook callback or Windows may remove
                // the hook as unresponsive.
                if (Interlocked.Exchange(ref _resolutionInProgress, 1) == 0)
                {
                    var point = mouse.Point;
                    _ = Task.Run(() =>
                    {
                        try
                        {
                            var itemPath = TryResolveExplorerItem(point);
                            if (!string.IsNullOrWhiteSpace(itemPath))
                                ItemActivated?.Invoke(this, itemPath);
                            else
                                ItemResolutionFailed?.Invoke(
                                    this,
                                    $"Fenêtre={GetWindowClass(rootWindow)}; Point={point.X},{point.Y}");
                        }
                        finally
                        {
                            Interlocked.Exchange(ref _resolutionInProgress, 0);
                        }
                    });
                }

                // Prevent Explorer from also opening the item in a new tab.
                return (IntPtr)1;
            }
        }

        return CallNextHookEx(_hook, code, message, data);
    }

    private static string? TryResolveExplorerItem(NativePoint point)
    {
        try
        {
            var window = GetAncestor(
                WindowFromPoint(point),
                GetAncestorFlags.Root);

            if (window == IntPtr.Zero || !IsSupportedShellWindow(window))
                return null;

            var elementAtPoint = AutomationElement.FromPoint(
                new System.Windows.Point(point.X, point.Y));

            var itemElement = FindExplorerItem(elementAtPoint);
            // Desktop icons do not expose exactly the same UI Automation tree
            // on every Windows 11 build.  When no ListItem ancestor is exposed,
            // the element directly under the pointer still carries the label.
            var names = GetCandidateNames(itemElement ?? elementAtPoint)
                .Concat(GetNamesFromItemsAtPoint(window, point))
                .SelectMany(GetNameVariants)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (names.Length == 0)
                return null;

            var folders = IsDesktopWindow(window)
                ? GetDesktopFolders()
                : [TryGetExplorerFolderPath(window)];

            foreach (var folderPath in folders.Where(Directory.Exists))
            {
                foreach (var name in names)
                {
                    var exactPath = Path.Combine(folderPath!, name);
                    if (File.Exists(exactPath) || Directory.Exists(exactPath))
                        return exactPath;

                    // Explorer can hide known file extensions. Match the visible
                    // label against both files and folders.
                    var match = Directory.EnumerateFileSystemEntries(folderPath!)
                        .FirstOrDefault(path =>
                            string.Equals(Path.GetFileName(path), name,
                                StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(Path.GetFileNameWithoutExtension(path), name,
                                StringComparison.OrdinalIgnoreCase));
                    if (match is not null)
                        return match;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<string> GetNamesFromItemsAtPoint(
        IntPtr shellWindow,
        NativePoint point)
    {
        AutomationElement? root;
        try
        {
            // The desktop icon list can live below another WorkerW than the
            // one returned by WindowFromPoint. Searching the automation root
            // and filtering by the click rectangle is reliable across both
            // Windows 10 and Windows 11 desktop implementations.
            root = IsDesktopWindow(shellWindow)
                ? AutomationElement.RootElement
                : AutomationElement.FromHandle(shellWindow);
        }
        catch
        {
            yield break;
        }

        var itemCondition = new OrCondition(
            new PropertyCondition(
                AutomationElement.ControlTypeProperty,
                ControlType.ListItem),
            new PropertyCondition(
                AutomationElement.ControlTypeProperty,
                ControlType.DataItem),
            new PropertyCondition(
                AutomationElement.ControlTypeProperty,
                ControlType.TreeItem));

        AutomationElementCollection items;
        try
        {
            items = root.FindAll(TreeScope.Descendants, itemCondition);
        }
        catch
        {
            yield break;
        }

        foreach (AutomationElement item in items)
        {
            System.Windows.Rect bounds;
            string name;
            try
            {
                bounds = item.Current.BoundingRectangle;
                name = item.Current.Name;
            }
            catch
            {
                continue;
            }

            if (!bounds.IsEmpty && bounds.Contains(point.X, point.Y) &&
                !string.IsNullOrWhiteSpace(name))
            {
                yield return name.Trim();
            }
        }
    }

    private static IEnumerable<string> GetNameVariants(string rawName)
    {
        var trimmed = rawName.Trim().Trim('"');
        if (trimmed.Length == 0)
            yield break;

        yield return trimmed;

        // Depending on the Windows 11 view, UI Automation can append status
        // information on a new line after the actual item name.
        var firstLine = trimmed.Split(['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(firstLine) &&
            !string.Equals(firstLine, trimmed, StringComparison.Ordinal))
        {
            yield return firstLine;
        }
    }

    private static IEnumerable<string?> GetDesktopFolders()
    {
        // DesktopDirectory follows OneDrive redirection when Windows has been
        // configured that way. CommonDesktopDirectory covers shared icons.
        yield return Environment.GetFolderPath(
            Environment.SpecialFolder.DesktopDirectory);
        yield return Environment.GetFolderPath(
            Environment.SpecialFolder.CommonDesktopDirectory);

        // Some OneDrive configurations do not update SpecialFolder reliably.
        // Read the canonical Windows redirection and common OneDrive fallbacks.
        using var userShellFolders = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders");
        if (userShellFolders?.GetValue("Desktop") is string redirected)
            yield return Environment.ExpandEnvironmentVariables(redirected);

        var oneDrive = Environment.GetEnvironmentVariable("OneDrive");
        if (!string.IsNullOrWhiteSpace(oneDrive))
        {
            yield return Path.Combine(oneDrive, "Desktop");
            yield return Path.Combine(oneDrive, "Bureau");
        }
    }

    private static IEnumerable<string> GetCandidateNames(AutomationElement element)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AutomationElement? current = element;

        // Climb through the raw UI Automation tree: Windows 11 can expose the
        // item name on the icon, its label, or one of their parent elements.
        // Current.Name is available through UIAutomationClient on every target
        // Windows version, unlike the optional legacy accessibility pattern.
        for (var level = 0; level < 16 && current is not null; level++)
        {
            AddName(current.Current.Name);
            current = TreeWalker.RawViewWalker.GetParent(current);
        }

        return seen;

        void AddName(string? value)
        {
            value = value?.Trim();
            if (!string.IsNullOrWhiteSpace(value) && value.Length <= 260)
                seen.Add(value);
        }
    }

    private static bool IsExplorerWindow(IntPtr window)
    {
        var className = new StringBuilder(256);
        var length = GetClassName(window, className, className.Capacity);
        if (length <= 0)
            return false;

        var value = className.ToString(0, length);
        return value is "CabinetWClass" or "ExploreWClass";
    }

    private static bool IsDesktopWindow(IntPtr window)
    {
        var className = new StringBuilder(256);
        var length = GetClassName(window, className, className.Capacity);
        if (length <= 0)
            return false;

        var value = className.ToString(0, length);
        return value is "Progman" or "WorkerW";
    }

    private static bool IsSupportedShellWindow(IntPtr window) =>
        IsExplorerWindow(window) || IsDesktopWindow(window);

    private static string GetWindowClass(IntPtr window)
    {
        var className = new StringBuilder(256);
        var length = GetClassName(window, className, className.Capacity);
        return length > 0 ? className.ToString(0, length) : "Inconnue";
    }

    private static AutomationElement? FindExplorerItem(AutomationElement element)
    {
        AutomationElement? current = element;

        // Windows 11 Explorer can insert several text/panel elements between
        // the point under the mouse and the actual ListItem/DataItem.
        for (var level = 0; level < 16 && current is not null; level++)
        {
            var type = current.Current.ControlType;
            if (type == ControlType.ListItem ||
                type == ControlType.DataItem ||
                type == ControlType.TreeItem)
            {
                return current.Current.IsEnabled ? current : null;
            }

            current = TreeWalker.ControlViewWalker.GetParent(current);
        }

        return null;
    }

    private static string? TryGetExplorerFolderPath(IntPtr explorerHandle)
    {
        Type? shellType = Type.GetTypeFromProgID("Shell.Application");
        if (shellType is null)
            return null;

        dynamic? shell = null;
        dynamic? windows = null;

        try
        {
            shell = Activator.CreateInstance(shellType);
            windows = shell?.Windows();
            if (windows is null)
                return null;

            for (var index = 0; index < windows.Count; index++)
            {
                dynamic? candidate = windows.Item(index);
                try
                {
                    if ((long)candidate.HWND == explorerHandle.ToInt64())
                        return (string?)candidate.Document.Folder.Self.Path;
                }
                finally
                {
                    ReleaseComObject(candidate);
                }
            }

            return null;
        }
        finally
        {
            ReleaseComObject(windows);
            ReleaseComObject(shell);
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
            Marshal.FinalReleaseComObject(value);
    }

    private delegate IntPtr LowLevelMouseProc(
        int code,
        IntPtr message,
        IntPtr data);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint
    {
        public readonly int X;
        public readonly int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct MsllHookStruct
    {
        public readonly NativePoint Point;
        public readonly uint MouseData;
        public readonly uint Flags;
        public readonly uint Time;
        public readonly UIntPtr ExtraInfo;
    }

    private enum GetAncestorFlags : uint
    {
        Root = 2
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(
        int hookId,
        LowLevelMouseProc callback,
        IntPtr module,
        uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(
        IntPtr hook,
        int code,
        IntPtr message,
        IntPtr data);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(NativePoint point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(
        IntPtr window,
        GetAncestorFlags flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(
        IntPtr window,
        StringBuilder className,
        int maximumCount);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
