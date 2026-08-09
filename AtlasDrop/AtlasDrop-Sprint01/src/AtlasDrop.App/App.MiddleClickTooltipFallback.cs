using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace AtlasDrop.App;

public partial class App
{
    private ExplorerMiddleClickTooltipFallback? _middleClickTooltipFallback;

    public App()
    {
        Startup += (_, _) =>
        {
            try
            {
                _middleClickTooltipFallback = new ExplorerMiddleClickTooltipFallback();
                _middleClickTooltipFallback.Start();
            }
            catch
            {
                _middleClickTooltipFallback?.Dispose();
                _middleClickTooltipFallback = null;
            }
        };

        Exit += (_, _) =>
        {
            _middleClickTooltipFallback?.Dispose();
            _middleClickTooltipFallback = null;
        };
    }
}

/// <summary>
/// Safety net for Windows 11 Explorer. When a tooltip is displayed over a
/// selected item, WindowFromPoint can report the tooltip instead of Explorer,
/// so the normal Atlas Drop middle-click hook never receives an Explorer root.
/// This fallback uses the foreground Explorer selection and forwards the item
/// through Atlas Drop's existing single-instance activation channel.
/// </summary>
internal sealed class ExplorerMiddleClickTooltipFallback : IDisposable
{
    private const int WhMouseLl = 14;
    private const int WmMButtonDown = 0x0207;
    private const uint GaRoot = 2;

    private readonly LowLevelMouseProc _callback;
    private IntPtr _hook;
    private int _activationInProgress;

    public ExplorerMiddleClickTooltipFallback()
    {
        _callback = OnMouseEvent;
    }

    public void Start()
    {
        if (_hook != IntPtr.Zero || !OperatingSystem.IsWindows())
            return;

        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule;
        var moduleHandle = GetModuleHandle(module?.ModuleName);
        _hook = SetWindowsHookEx(WhMouseLl, _callback, moduleHandle, 0);

        if (_hook == IntPtr.Zero)
            throw new InvalidOperationException("Impossible d'activer le secours clic molette Atlas Drop.");
    }

    public void Dispose()
    {
        if (_hook == IntPtr.Zero)
            return;

        UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    private IntPtr OnMouseEvent(int code, IntPtr message, IntPtr data)
    {
        if (code < 0 || message != (IntPtr)WmMButtonDown)
            return CallNextHookEx(_hook, code, message, data);

        var mouse = Marshal.PtrToStructure<MsllHookStruct>(data);
        var directRoot = GetAncestor(WindowFromPoint(mouse.Point), GaRoot);

        // Normal case: the existing ExplorerMiddleClickActivation owns it.
        if (IsExplorerWindow(directRoot))
            return CallNextHookEx(_hook, code, message, data);

        var foreground = GetAncestor(GetForegroundWindow(), GaRoot);
        if (!IsExplorerWindow(foreground) || !IsPointInsideWindow(foreground, mouse.Point))
            return CallNextHookEx(_hook, code, message, data);

        if (Interlocked.Exchange(ref _activationInProgress, 1) == 0)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    var selectedPath = TryGetSingleSelectedExplorerItem(foreground);
                    if (!string.IsNullOrWhiteSpace(selectedPath) &&
                        (File.Exists(selectedPath) || Directory.Exists(selectedPath)))
                    {
                        var executable = Environment.ProcessPath;
                        if (!string.IsNullOrWhiteSpace(executable))
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = executable,
                                ArgumentList = { selectedPath },
                                UseShellExecute = false
                            });
                        }
                    }
                }
                catch
                {
                }
                finally
                {
                    Interlocked.Exchange(ref _activationInProgress, 0);
                }
            });
        }

        // Do not let Explorer interpret the middle click while the fallback
        // resolves the already-selected item.
        return (IntPtr)1;
    }

    private static string? TryGetSingleSelectedExplorerItem(IntPtr explorerHandle)
    {
        var shellType = Type.GetTypeFromProgID("Shell.Application");
        if (shellType is null)
            return null;

        object? shell = null;
        object? windows = null;
        try
        {
            shell = Activator.CreateInstance(shellType);
            windows = ((dynamic)shell!).Windows();
            var count = Convert.ToInt32(((dynamic)windows).Count);

            for (var index = 0; index < count; index++)
            {
                object? candidate = null;
                object? selectedItems = null;
                object? selectedItem = null;
                try
                {
                    candidate = ((dynamic)windows).Item(index);
                    if (candidate is null || Convert.ToInt64(((dynamic)candidate).HWND) != explorerHandle.ToInt64())
                        continue;

                    selectedItems = ((dynamic)candidate).Document.SelectedItems();
                    if (selectedItems is null || Convert.ToInt32(((dynamic)selectedItems).Count) != 1)
                        return null;

                    selectedItem = ((dynamic)selectedItems).Item(0);
                    return selectedItem is null ? null : (string?)((dynamic)selectedItem).Path;
                }
                catch
                {
                    return null;
                }
                finally
                {
                    ReleaseComObject(selectedItem);
                    ReleaseComObject(selectedItems);
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

    private static bool IsExplorerWindow(IntPtr window)
    {
        if (window == IntPtr.Zero)
            return false;

        var className = new StringBuilder(256);
        var length = GetClassName(window, className, className.Capacity);
        if (length <= 0)
            return false;

        var value = className.ToString(0, length);
        return value is "CabinetWClass" or "ExploreWClass";
    }

    private static bool IsPointInsideWindow(IntPtr window, NativePoint point)
    {
        return GetWindowRect(window, out var rect) &&
               point.X >= rect.Left && point.X < rect.Right &&
               point.Y >= rect.Top && point.Y < rect.Bottom;
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            try { Marshal.FinalReleaseComObject(value); } catch { }
        }
    }

    private delegate IntPtr LowLevelMouseProc(int code, IntPtr message, IntPtr data);

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

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookId, LowLevelMouseProc callback, IntPtr module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(NativePoint point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr window, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, StringBuilder className, int maximumCount);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
