using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace QuickDock.Services;

public static class DwmHelper
{
    // DWM Window Attributes
    private const int DWMWA_TRANSITIONS_FORCEDISABLED = 3;
    private const int DWMWA_DISALLOW_PEEK = 11;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

    // Corner preferences
    private const int DWMWCP_DEFAULT = 0;
    private const int DWMWCP_DONOTROUND = 1;
    private const int DWMWCP_ROUND = 2;
    private const int DWMWCP_ROUNDSMALL = 3;

    // Backdrop types
    private const int DWMSBT_AUTO = 0;
    private const int DWMSBT_NONE = 1;
    private const int DWMSBT_MAINWINDOW = 2;      // Mica
    private const int DWMSBT_TRANSIENTWINDOW = 3; // Transient Acrylic (ideal for popups / flyouts)
    private const int DWMSBT_TABBEDWINDOW = 4;    // Mica Alt

    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int MDT_EFFECTIVE_DPI = 0;
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_LAYERED = 0x00080000;

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLongPtr32(hWnd, nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong) =>
        IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : SetWindowLongPtr32(hWnd, nIndex, dwNewLong);

    [StructLayout(LayoutKind.Sequential)]
    public struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    public const uint SHGFI_ICON = 0x000000100;
    public const uint SHGFI_LARGEICON = 0x000000000;
    public const uint SHGFI_SMALLICON = 0x000000001;
    public const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
    public const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("dwmapi.dll")]
    public static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("shcore.dll")]
    public static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref SHFILEINFO psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyIcon(IntPtr hIcon);

    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr ProcessId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, [MarshalAs(UnmanagedType.Bool)] bool fAttach);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("shell32.dll", SetLastError = true)]
    public static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

    private static readonly object _logLock = new();

    [DllImport("user32.dll")]
    private static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

    [DllImport("user32.dll")]
    private static extern IntPtr SetCursor(IntPtr hCursor);

    public const int IDC_ARROW = 32512;

    /// <summary>
    /// Restores the standard Windows arrow cursor.
    /// </summary>
    public static void RestoreStandardCursor()
    {
        try
        {
            IntPtr arrow = LoadCursor(IntPtr.Zero, IDC_ARROW);
            if (arrow != IntPtr.Zero)
            {
                SetCursor(arrow);
            }
        }
        catch {}
    }

    public static void ForceForeground(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;

        IntPtr foregroundHwnd = GetForegroundWindow();
        uint foregroundThreadId = GetWindowThreadProcessId(foregroundHwnd, IntPtr.Zero);
        uint currentThreadId = GetCurrentThreadId();

        if (foregroundThreadId != 0 && foregroundThreadId != currentThreadId)
        {
            AttachThreadInput(currentThreadId, foregroundThreadId, true);
            BringWindowToTop(hwnd);
            ShowWindow(hwnd, 5); // SW_SHOW
            SetForegroundWindow(hwnd);
            AttachThreadInput(currentThreadId, foregroundThreadId, false);
        }
        else
        {
            BringWindowToTop(hwnd);
            ShowWindow(hwnd, 5);
            SetForegroundWindow(hwnd);
        }
    }

    public static void Log(string msg)
    {
        try
        {
            lock (_logLock)
            {
                string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.log");
                var fi = new FileInfo(p);
                if (fi.Exists && fi.Length > 1_000_000)
                {
                    string old = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.log.old");
                    File.Move(p, old, overwrite: true);
                }
                File.AppendAllText(p, $"[{DateTime.Now:HH:mm:ss.fff}] [PID:{Environment.ProcessId}] {msg}\n");
            }
        }
        catch {}
    }

    /// <summary>
    /// Checks if Windows is currently in Dark Mode.
    /// </summary>
    public static bool IsDarkModeEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int val)
            {
                return val == 0;
            }
        }
        catch
        {
            // Default to dark mode if registry cannot be read
        }
        return true;
    }

    /// <summary>
    /// Restricts DWM Aero Peek for this window.
    /// </summary>
    public static void DisableTaskbarThumbnailPreview(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return;

        try
        {
            int disallowPeek = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_DISALLOW_PEEK, ref disallowPeek, sizeof(int));

            int excludedFromPeek = 1;
            const int DWMWA_EXCLUDED_FROM_PEEK = 12;
            DwmSetWindowAttribute(hwnd, DWMWA_EXCLUDED_FROM_PEEK, ref excludedFromPeek, sizeof(int));
        }
        catch (Exception ex)
        {
            Log($"DisableTaskbarThumbnailPreview error: {ex.Message}");
        }
    }

    /// <summary>
    /// Applies Windows 11 dark/light mode and disables OS-level rectangular transitions
    /// without creating any rectangular background or shadow artifacts.
    /// </summary>
    public static void ApplyWindowAttributes(IntPtr hwnd, bool isDark)
    {
        if (hwnd == IntPtr.Zero) return;

        // Disable OS-level rectangular transitions for custom WPF fluid animations
        int disableTransitions = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_TRANSITIONS_FORCEDISABLED, ref disableTransitions, sizeof(int));

        // Immersive Dark Mode
        int darkMode = isDark ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

        // Restrict DWM Peek
        DisableTaskbarThumbnailPreview(hwnd);
    }

    public static void ApplyTransientAcrylic(IntPtr hwnd, bool isDark) => ApplyWindowAttributes(hwnd, isDark);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT Point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

    private const uint GA_ROOT = 2;

    private static readonly object _taskbarCacheLock = new();
    private static readonly System.Collections.Generic.Dictionary<IntPtr, (System.Windows.Rect Rect, long Tick)> _lastTaskbarButtonByMonitor = new();

    private static bool IsTaskbarWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return false;
        IntPtr root = GetAncestor(hwnd, GA_ROOT);
        if (root == IntPtr.Zero) root = hwnd;
        var sb = new System.Text.StringBuilder(64);
        if (GetClassName(root, sb, sb.Capacity) <= 0) return false;
        string cls = sb.ToString();
        return cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd";
    }

    private static void CacheTaskbarButton(IntPtr hMonitor, System.Windows.Rect rect)
    {
        if (hMonitor == IntPtr.Zero) return;
        lock (_taskbarCacheLock)
        {
            _lastTaskbarButtonByMonitor[hMonitor] = (rect, Environment.TickCount64);
        }
    }

    private static bool TryGetCachedTaskbarButton(IntPtr hMonitor, long maxAgeMs, out System.Windows.Rect rect)
    {
        rect = default;
        if (hMonitor == IntPtr.Zero) return false;
        lock (_taskbarCacheLock)
        {
            if (_lastTaskbarButtonByMonitor.TryGetValue(hMonitor, out var entry))
            {
                if (maxAgeMs < 0 || (Environment.TickCount64 - entry.Tick) <= maxAgeMs)
                {
                    rect = entry.Rect;
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Attempts to find the exact taskbar button element under or near the specified physical screen point.
    /// Excludes elements owned by this application to avoid self-snapping.
    /// </summary>
    public static System.Windows.Rect? GetTaskbarButtonNear(double x, double y)
    {
        try
        {
            IntPtr hwndUnderPoint = WindowFromPoint(new POINT { X = (int)Math.Round(x), Y = (int)Math.Round(y) });
            if (!IsTaskbarWindow(hwndUnderPoint))
            {
                return null;
            }

            var el = AutomationElement.FromPoint(new System.Windows.Point(x, y));
            if (el != null)
            {
                if (el.Current.ProcessId == Environment.ProcessId)
                {
                    return null;
                }

                var r = el.Current.BoundingRectangle;
                if (!r.IsEmpty && r.Width >= 16 && r.Height >= 16 && r.Width <= 250 && r.Height <= 120)
                {
                    return new System.Windows.Rect(r.X, r.Y, r.Width, r.Height);
                }

                // Check parent if the element under cursor is an image/icon inside the button
                var parent = TreeWalker.ControlViewWalker.GetParent(el);
                if (parent != null)
                {
                    if (parent.Current.ProcessId == Environment.ProcessId)
                    {
                        return null;
                    }

                    var pr = parent.Current.BoundingRectangle;
                    if (!pr.IsEmpty && pr.Width >= 16 && pr.Height >= 16 && pr.Width <= 250 && pr.Height <= 120)
                    {
                        return new System.Windows.Rect(pr.X, pr.Y, pr.Width, pr.Height);
                    }
                }
            }
        }
        catch {}

        return null;
    }

    /// <summary>
    /// Searches the Windows taskbar on the specified monitor for the QuickDock taskbar button.
    /// Used when the cursor moved off the taskbar before GetCursorPos ran (e.g. cold start).
    /// </summary>
    public static System.Windows.Rect? FindQuickDockTaskbarButton(IntPtr hMonitor)
    {
        if (hMonitor == IntPtr.Zero) return null;

        // Reuse very recent lookup during multi-pass RepositionWindow calls in a single flyout open
        if (TryGetCachedTaskbarButton(hMonitor, maxAgeMs: 500, out var recentRect))
        {
            return recentRect;
        }

        try
        {
            var trayWindows = new System.Collections.Generic.List<IntPtr>();
            IntPtr primaryTray = FindWindow("Shell_TrayWnd", null);
            if (primaryTray != IntPtr.Zero)
                trayWindows.Add(primaryTray);

            IntPtr secTray = IntPtr.Zero;
            while ((secTray = FindWindowEx(IntPtr.Zero, secTray, "Shell_SecondaryTrayWnd", null)) != IntPtr.Zero)
            {
                trayWindows.Add(secTray);
            }

            var buttonCond = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button);
            System.Windows.Rect? nameFallback = null;

            foreach (IntPtr trayHwnd in trayWindows)
            {
                if (MonitorFromWindow(trayHwnd, MONITOR_DEFAULTTONEAREST) != hMonitor)
                    continue;

                var trayEl = AutomationElement.FromHandle(trayHwnd);
                if (trayEl == null) continue;

                var buttons = trayEl.FindAll(TreeScope.Descendants, buttonCond);
                for (int i = 0; i < buttons.Count; i++)
                {
                    var btn = buttons[i];
                    try
                    {
                        if (btn.Current.ProcessId == Environment.ProcessId)
                            continue;

                        var r = btn.Current.BoundingRectangle;
                        if (r.IsEmpty || r.Width < 16 || r.Height < 16 || r.Width > 250 || r.Height > 120)
                            continue;

                        string autoId = btn.Current.AutomationId ?? string.Empty;
                        string name = btn.Current.Name ?? string.Empty;

                        if (autoId.Contains("QuickDock.App", StringComparison.OrdinalIgnoreCase))
                        {
                            var rect = new System.Windows.Rect(r.X, r.Y, r.Width, r.Height);
                            CacheTaskbarButton(hMonitor, rect);
                            return rect;
                        }

                        if (!nameFallback.HasValue &&
                            (name.Equals("QuickDock", StringComparison.OrdinalIgnoreCase) ||
                             name.StartsWith("QuickDock - ", StringComparison.OrdinalIgnoreCase) ||
                             name.StartsWith("QuickDock (", StringComparison.OrdinalIgnoreCase)))
                        {
                            nameFallback = new System.Windows.Rect(r.X, r.Y, r.Width, r.Height);
                        }
                    }
                    catch {}
                }
            }

            if (nameFallback.HasValue)
            {
                CacheTaskbarButton(hMonitor, nameFallback.Value);
                return nameFallback;
            }
        }
        catch {}

        return null;
    }

    /// <summary>
    /// Computes a resolution- and aspect-ratio-aware maximum flyout height (in DIPs) for the monitor
    /// under the target cursor position so taller/higher-res screens allow more items without clipping.
    /// </summary>
    public static double GetRecommendedMaxFlyoutHeightDip(double? preferredCursorX, double? preferredCursorY)
    {
        double physicalX;
        double physicalY;

        if (preferredCursorX.HasValue && preferredCursorY.HasValue &&
            !double.IsNaN(preferredCursorX.Value) && !double.IsNaN(preferredCursorY.Value))
        {
            physicalX = preferredCursorX.Value;
            physicalY = preferredCursorY.Value;
        }
        else
        {
            GetCursorPos(out POINT cursorPt);
            physicalX = cursorPt.X;
            physicalY = cursorPt.Y;
        }

        POINT pt = new POINT { X = (int)Math.Round(physicalX), Y = (int)Math.Round(physicalY) };
        IntPtr hMonitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
        MONITORINFO mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };

        double dpiScaleX = 1.0;
        double dpiScaleY = 1.0;

        if (hMonitor != IntPtr.Zero)
        {
            try
            {
                if (GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, out uint dpiX, out uint dpiY) == 0)
                {
                    dpiScaleX = dpiX / 96.0;
                    dpiScaleY = dpiY / 96.0;
                }
            }
            catch {}
        }

        double workWidthDip;
        double workHeightDip;
        if (hMonitor != IntPtr.Zero && GetMonitorInfo(hMonitor, ref mi))
        {
            workWidthDip = (mi.rcWork.Right - mi.rcWork.Left) / dpiScaleX;
            workHeightDip = (mi.rcWork.Bottom - mi.rcWork.Top) / dpiScaleY;
        }
        else
        {
            var wa = SystemParameters.WorkArea;
            workWidthDip = wa.Width;
            workHeightDip = wa.Height;
        }

        if (workWidthDip <= 0 || workHeightDip <= 0)
            return 560;

        // Scale the height ratio slightly higher on taller aspect ratios (e.g. 16:10 / 3:2 laptop displays vs 16:9)
        double aspectRatio = workHeightDip / workWidthDip;
        double heightFraction = Math.Clamp(0.54 + Math.Max(0.0, aspectRatio - 0.5625) * 0.45, 0.54, 0.68);
        double maxAllowed = Math.Max(260.0, workHeightDip - 32.0);
        return Math.Round(Math.Clamp(workHeightDip * heightFraction, 360.0, Math.Min(720.0, maxAllowed)));
    }

    /// <summary>
    /// Calculates the optimal physical screen pixel position for the flyout window,
    /// centered above/beside the QuickDock taskbar icon and strictly clamped within a single monitor's work area.
    /// </summary>
    public static POINT CalculateFlyoutPhysicalPosition(
        double windowWidthDip,
        double windowHeightDip,
        double? preferredCursorX,
        double? preferredCursorY,
        out double dpiScaleX,
        out double dpiScaleY)
    {
        dpiScaleX = 1.0;
        dpiScaleY = 1.0;

        double physicalX;
        double physicalY;
        bool hasExplicitCursor = preferredCursorX.HasValue && preferredCursorY.HasValue &&
                                 !double.IsNaN(preferredCursorX.Value) && !double.IsNaN(preferredCursorY.Value) &&
                                 !(preferredCursorX.Value == 0 && preferredCursorY.Value == 0);

        if (hasExplicitCursor)
        {
            physicalX = preferredCursorX!.Value;
            physicalY = preferredCursorY!.Value;
            Log($"CalculateFlyoutPosition: Using preferred click cursor ({physicalX}, {physicalY})");
        }
        else
        {
            GetCursorPos(out POINT cursorPt);
            physicalX = cursorPt.X;
            physicalY = cursorPt.Y;
            Log($"CalculateFlyoutPosition: Using live cursor ({physicalX}, {physicalY})");
        }

        POINT pt = new POINT { X = (int)Math.Round(physicalX), Y = (int)Math.Round(physicalY) };
        IntPtr hMonitor = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
        MONITORINFO mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };

        // Try to snap to the exact taskbar button under the cursor, or locate QuickDock's taskbar button on this monitor
        var btnRect = GetTaskbarButtonNear(physicalX, physicalY);
        if (btnRect.HasValue)
        {
            CacheTaskbarButton(hMonitor, btnRect.Value);
            physicalX = btnRect.Value.Left + (btnRect.Value.Width / 2.0);
            physicalY = btnRect.Value.Top;
            Log($"CalculateFlyoutPosition: Snapped to button bounds at ({physicalX}, {physicalY})");
        }
        else
        {
            btnRect = FindQuickDockTaskbarButton(hMonitor);
            if (btnRect.HasValue)
            {
                physicalX = btnRect.Value.Left + (btnRect.Value.Width / 2.0);
                physicalY = btnRect.Value.Top;
                Log($"CalculateFlyoutPosition: Located QuickDock taskbar button at ({physicalX}, {physicalY})");
            }
        }

        if (hMonitor != IntPtr.Zero)
        {
            try
            {
                if (GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, out uint dpiX, out uint dpiY) == 0)
                {
                    dpiScaleX = dpiX / 96.0;
                    dpiScaleY = dpiY / 96.0;
                }
            }
            catch {}
        }

        if (hMonitor == IntPtr.Zero || !GetMonitorInfo(hMonitor, ref mi))
        {
            var wa = SystemParameters.WorkArea;
            int fx = (int)Math.Round((wa.Left + wa.Right - windowWidthDip) / 2.0);
            int fy = (int)Math.Round(wa.Bottom - windowHeightDip - 8.0);
            return new POINT { X = fx, Y = fy };
        }

        // Perform all bounds & clamping in physical pixels so mixed-DPI monitors never misalign or straddle
        double physWidth = windowWidthDip * dpiScaleX;
        double physHeight = windowHeightDip * dpiScaleY;
        double marginX = 8.0 * dpiScaleX;
        double marginY = 8.0 * dpiScaleY;

        double workLeft = mi.rcWork.Left;
        double workRight = mi.rcWork.Right;
        double workTop = mi.rcWork.Top;
        double workBottom = mi.rcWork.Bottom;
        double monitorLeft = mi.rcMonitor.Left;
        double monitorRight = mi.rcMonitor.Right;
        double monitorTop = mi.rcMonitor.Top;
        double monitorBottom = mi.rcMonitor.Bottom;

        // Check whether the click actually came from the taskbar band (including auto-hide edge).
        // If not (e.g. cursor moved off taskbar before startup completed), fall back to cached button bounds if available.
        double autoHideBand = 60.0 * dpiScaleY;
        bool hasValidCursor = !(physicalX == 0 && physicalY == 0);
        bool isClickOnTaskbar = hasValidCursor && (btnRect.HasValue ||
            (physicalY >= workBottom && physicalY <= monitorBottom) ||
            (physicalY >= monitorTop && physicalY < workTop) ||
            (physicalX >= monitorLeft && physicalX < workLeft) ||
            (physicalX >= workRight && physicalX <= monitorRight) ||
            (Math.Abs(workBottom - monitorBottom) < 1.0 && physicalY >= monitorBottom - autoHideBand));

        if (!btnRect.HasValue && !isClickOnTaskbar && TryGetCachedTaskbarButton(hMonitor, maxAgeMs: -1, out var cachedBtnRect))
        {
            btnRect = cachedBtnRect;
            physicalX = cachedBtnRect.Left + (cachedBtnRect.Width / 2.0);
            physicalY = cachedBtnRect.Top;
            isClickOnTaskbar = true;
            Log($"CalculateFlyoutPosition: Fallback to cached taskbar button at ({physicalX}, {physicalY})");
        }

        double targetLeft;
        if (workLeft > monitorLeft + 1.0)
        {
            // Taskbar is docked on the left screen edge
            targetLeft = workLeft + marginX;
        }
        else if (workRight < monitorRight - 1.0)
        {
            // Taskbar is docked on the right screen edge
            targetLeft = workRight - physWidth - marginX;
        }
        else if (!isClickOnTaskbar)
        {
            // Centered horizontally on the monitor when not invoked by clicking a taskbar button
            targetLeft = workLeft + ((workRight - workLeft - physWidth) / 2.0);
        }
        else
        {
            // Centered directly above the click / icon position
            targetLeft = physicalX - (physWidth / 2.0);
        }

        // Clamp horizontally within the single monitor's working area
        if (targetLeft < workLeft + marginX)
            targetLeft = workLeft + marginX;
        if (targetLeft + physWidth > workRight - marginX)
            targetLeft = workRight - marginX - physWidth;

        // Vertically: float 8 DIPs above bottom taskbar, or 8 DIPs below top taskbar
        double targetTop;
        if (btnRect.HasValue)
        {
            if (workTop > monitorTop + 1.0)
            {
                targetTop = btnRect.Value.Bottom + marginY;
            }
            else
            {
                targetTop = btnRect.Value.Top - physHeight - marginY;
            }
        }
        else if (workTop > monitorTop + 1.0)
        {
            targetTop = workTop + marginY;
        }
        else
        {
            targetTop = workBottom - physHeight - marginY;
        }

        // Clamp vertically within the single monitor's working area
        if (targetTop < workTop + marginY)
            targetTop = workTop + marginY;
        if (targetTop + physHeight > workBottom - marginY)
            targetTop = workBottom - marginY - physHeight;

        int finalX = (int)Math.Round(targetLeft);
        int finalY = (int)Math.Round(targetTop);
        Log($"CalculateFlyoutPosition: Final Phys Pos=({finalX}, {finalY}) for cursor=({physicalX},{physicalY}), DpiScale=({dpiScaleX},{dpiScaleY})");
        return new POINT { X = finalX, Y = finalY };
    }

    public static System.Windows.Point CalculateFlyoutPosition(
        double windowWidth,
        double windowHeight,
        double? preferredCursorX,
        double? preferredCursorY,
        out double dpiScaleX,
        out double dpiScaleY)
    {
        var phys = CalculateFlyoutPhysicalPosition(windowWidth, windowHeight, preferredCursorX, preferredCursorY, out dpiScaleX, out dpiScaleY);
        return new System.Windows.Point(phys.X / dpiScaleX, phys.Y / dpiScaleY);
    }

    public static System.Windows.Point CalculateFlyoutPosition(double windowWidth, double windowHeight, double? preferredCursorX = null, double? preferredCursorY = null)
    {
        return CalculateFlyoutPosition(windowWidth, windowHeight, preferredCursorX, preferredCursorY, out _, out _);
    }

    /// <summary>
    /// Extracts the native Windows shell folder icon for a given path.
    /// </summary>
    public static ImageSource? ExtractFolderIcon(string path)
    {
        try
        {
            SHFILEINFO shinfo = new SHFILEINFO();
            IntPtr result = IntPtr.Zero;

            // First attempt: direct path to retrieve custom folder icons (e.g. Downloads, Pictures, etc.)
            if (Directory.Exists(path))
            {
                result = SHGetFileInfo(
                    path,
                    0,
                    ref shinfo,
                    (uint)Marshal.SizeOf(shinfo),
                    SHGFI_ICON | SHGFI_LARGEICON);
            }

            // Fallback: generic directory icon
            if (result == IntPtr.Zero || shinfo.hIcon == IntPtr.Zero)
            {
                result = SHGetFileInfo(
                    path,
                    FILE_ATTRIBUTE_DIRECTORY,
                    ref shinfo,
                    (uint)Marshal.SizeOf(shinfo),
                    SHGFI_ICON | SHGFI_USEFILEATTRIBUTES | SHGFI_LARGEICON);
            }

            if (shinfo.hIcon != IntPtr.Zero)
            {
                try
                {
                    var bitmap = Imaging.CreateBitmapSourceFromHIcon(
                        shinfo.hIcon,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    bitmap.Freeze(); // Ensure cross-thread and high-performance safety
                    return bitmap;
                }
                finally
                {
                    DestroyIcon(shinfo.hIcon);
                }
            }
        }
        catch (Exception ex)
        {
            Log($"ExtractFolderIcon error for '{path}': {ex.Message}");
        }

        return null;
    }
}
