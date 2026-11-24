using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using QuickDock.Models;
using QuickDock.Services;
using Application = System.Windows.Application;

namespace QuickDock;

public partial class MainWindow : Window
{
    private bool _isClosing = false;
    private bool _isOpening = false;
    private bool _isDialogOpen = false;
    private bool _isContextMenuOpen = false;
    private long _lastShowTick = 0;
    private long _lastHideTick = 0;
    private long _dialogClosedTick = 0;
    private long _contextMenuClosedTick = 0;
    private double? _activeCursorX;
    private double? _activeCursorY;
    private double _cachedFlyoutHeight = 380;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyTheme(DwmHelper.IsDarkModeEnabled());
        _ = LoadFoldersAsync();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var helper = new WindowInteropHelper(this);
        IntPtr hwnd = helper.Handle;

        // Apply dark mode and window settings without rectangular OS artifacts
        DwmHelper.ApplyWindowAttributes(hwnd, DwmHelper.IsDarkModeEnabled());
    }

    /// <summary>
    /// Loads configured folders from storage asynchronously.
    /// </summary>
    public async Task LoadFoldersAsync(bool force = false)
    {
        try
        {
            var folders = await FolderConfigService.GetConfiguredFoldersAsync(forceReload: force);
            DwmHelper.Log($"LoadFoldersAsync returned {folders?.Count ?? -1} items");
            
            FoldersList.ItemsSource = null;
            FoldersList.ItemsSource = folders;

            SettingsFoldersList.ItemsSource = null;
            SettingsFoldersList.ItemsSource = folders;

            bool isEmpty = folders == null || folders.Count == 0;
            EmptyStateText.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
            FoldersScrollViewer.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;
            SettingsEmptyStateText.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;

            if (WindowState == WindowState.Normal && !_isOpening)
            {
                RepositionWindow();
            }
        }
        catch (Exception ex)
        {
            DwmHelper.Log($"Failed to load folders: {ex.Message}");
        }
    }

    /// <summary>
    /// Applies Light / Dark theme colors to brushes and DWM frame.
    /// </summary>
    public void ApplyTheme(bool isDark)
    {
        var helper = new WindowInteropHelper(this);
        if (helper.Handle != IntPtr.Zero)
        {
            DwmHelper.ApplyWindowAttributes(helper.Handle, isDark);
        }

        if (isDark)
        {
            Resources["WindowTintBrush"] = CreateBrush("#EE1E1E1E");
            Resources["InnerBorderBrush"] = CreateBrush("#28FFFFFF");
            Resources["TextPrimaryBrush"] = CreateBrush("#F2FFFFFF");
            Resources["TextSecondaryBrush"] = CreateBrush("#85FFFFFF");
            Resources["RowHoverBrush"] = CreateBrush("#14FFFFFF");
            Resources["ButtonHoverBrush"] = CreateBrush("#28FFFFFF");
            Resources["ToolTipBackgroundBrush"] = CreateBrush("#F0202020");
            Resources["ToolTipBorderBrush"] = CreateBrush("#25FFFFFF");
            Resources["ScrollBarThumbBrush"] = CreateBrush("#35888888");
            Resources["ScrollBarThumbHoverBrush"] = CreateBrush("#65888888");
            Resources["DangerHoverBrush"] = CreateBrush("#35FF4444");
        }
        else
        {
            Resources["WindowTintBrush"] = CreateBrush("#EEF6F6F6");
            Resources["InnerBorderBrush"] = CreateBrush("#1E000000");
            Resources["TextPrimaryBrush"] = CreateBrush("#E4000000");
            Resources["TextSecondaryBrush"] = CreateBrush("#80000000");
            Resources["RowHoverBrush"] = CreateBrush("#0A000000");
            Resources["ButtonHoverBrush"] = CreateBrush("#16000000");
            Resources["ToolTipBackgroundBrush"] = CreateBrush("#F8F9FAFA");
            Resources["ToolTipBorderBrush"] = CreateBrush("#18000000");
            Resources["ScrollBarThumbBrush"] = CreateBrush("#30000000");
            Resources["ScrollBarThumbHoverBrush"] = CreateBrush("#55000000");
            Resources["DangerHoverBrush"] = CreateBrush("#25FF2222");
        }
    }

    private static SolidColorBrush CreateBrush(string hex)
    {
        var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    /// <summary>
    /// Switches to the Settings / Configure view.
    /// </summary>
    public void ShowSettingsView()
    {
        FavoritesHeader.Visibility = Visibility.Collapsed;
        FavoritesPanel.Visibility = Visibility.Collapsed;
        SettingsHeader.Visibility = Visibility.Visible;
        SettingsPanel.Visibility = Visibility.Visible;

        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        SettingsPanel.BeginAnimation(UIElement.OpacityProperty, fadeIn);

        _ = LoadFoldersAsync(force: false);
        RepositionWindow();
    }

    /// <summary>
    /// Switches to the Favorites view.
    /// </summary>
    public void ShowFavoritesView()
    {
        SettingsHeader.Visibility = Visibility.Collapsed;
        SettingsPanel.Visibility = Visibility.Collapsed;
        FavoritesHeader.Visibility = Visibility.Visible;
        FavoritesPanel.Visibility = Visibility.Visible;

        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        FavoritesPanel.BeginAnimation(UIElement.OpacityProperty, fadeIn);

        RepositionWindow();
    }

    /// <summary>
    /// Displays the airy flyout with authentic Windows 11 Fluent entrance motion.
    /// </summary>
    public async void ShowFlyout(double? cursorX = null, double? cursorY = null)
    {
        DwmHelper.Log($"ShowFlyout entry, WindowState={WindowState}, isClosing={_isClosing}, Cursor=({cursorX},{cursorY})");

        if (cursorX.HasValue && cursorY.HasValue && !double.IsNaN(cursorX.Value) && !double.IsNaN(cursorY.Value))
        {
            _activeCursorX = cursorX;
            _activeCursorY = cursorY;
        }
        else
        {
            DwmHelper.GetCursorPos(out var pt);
            _activeCursorX = pt.X;
            _activeCursorY = pt.Y;
        }

        _isClosing = false;
        _isOpening = true;
        _lastShowTick = Environment.TickCount64;

        ApplyTheme(DwmHelper.IsDarkModeEnabled());

        if (WindowState != WindowState.Normal)
        {
            WindowState = WindowState.Normal;
        }

        // Reposition to the exact location of the taskbar icon before animation
        RepositionWindow();

        RootBorder.BeginAnimation(UIElement.OpacityProperty, null);
        RootTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

        RootBorder.Opacity = 0;
        RootTranslate.Y = 16;
        RootScale.ScaleX = 0.96;
        RootScale.ScaleY = 0.96;

        Show();

        var helper = new WindowInteropHelper(this);
        if (helper.Handle != IntPtr.Zero)
        {
            DwmHelper.ForceForeground(helper.Handle);
            DwmHelper.DisableTaskbarThumbnailPreview(helper.Handle);
        }

        Activate();
        Focus();

        var ease = new CircleEase { EasingMode = EasingMode.EaseOut };
        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease };
        var slideUp = new DoubleAnimation(16, 0, TimeSpan.FromMilliseconds(240)) { EasingFunction = ease };
        var scaleX = new DoubleAnimation(0.96, 1.0, TimeSpan.FromMilliseconds(240)) { EasingFunction = ease };
        var scaleY = new DoubleAnimation(0.96, 1.0, TimeSpan.FromMilliseconds(240)) { EasingFunction = ease };

        RootBorder.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        RootTranslate.BeginAnimation(TranslateTransform.YProperty, slideUp);
        RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
        RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);

        _ = LoadFoldersAsync(force: false);

        await Task.Delay(240);
        _isOpening = false;

        // If flyout was dismissed while opening animation was running, do not reposition or interfere
        if (Visibility != Visibility.Visible || _isClosing)
            return;

        // Ensure window position perfectly reflects final rendered content height
        RepositionWindow();
    }

    /// <summary>
    /// Opens the flyout and immediately shows the Settings menu.
    /// </summary>
    public void OpenSettingsDirectly(double? cursorX = null, double? cursorY = null)
    {
        ShowFlyout(cursorX, cursorY);
        ShowSettingsView();
    }

    /// <summary>
    /// Dismisses the flyout smoothly to the taskbar.
    /// </summary>
    public async void HideFlyout()
    {
        DwmHelper.Log($"HideFlyout entry, Visibility={Visibility}, WindowState={WindowState}, isClosing={_isClosing}");
        if (Visibility != Visibility.Visible || _isClosing)
            return;

        _isClosing = true;
        _isOpening = false;
        _lastHideTick = Environment.TickCount64;

        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(130)) { EasingFunction = ease };
        var slideDown = new DoubleAnimation(0, 10, TimeSpan.FromMilliseconds(130)) { EasingFunction = ease };
        var scaleX = new DoubleAnimation(1.0, 0.97, TimeSpan.FromMilliseconds(130)) { EasingFunction = ease };
        var scaleY = new DoubleAnimation(1.0, 0.97, TimeSpan.FromMilliseconds(130)) { EasingFunction = ease };

        RootBorder.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        RootTranslate.BeginAnimation(TranslateTransform.YProperty, slideDown);
        RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
        RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);

        await Task.Delay(135);

        // If ShowFlyout was invoked while animating out, abort hiding
        if (!_isClosing || _isOpening)
            return;

        Hide();

        // Reset to Favorites view for next time
        SettingsHeader.Visibility = Visibility.Collapsed;
        SettingsPanel.Visibility = Visibility.Collapsed;
        FavoritesHeader.Visibility = Visibility.Visible;
        FavoritesPanel.Visibility = Visibility.Visible;

        RootBorder.BeginAnimation(UIElement.OpacityProperty, null);
        RootTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        RootScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        RootScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

        RootBorder.Opacity = 1;
        RootTranslate.Y = 0;
        RootScale.ScaleX = 1;
        RootScale.ScaleY = 1;

        _activeCursorX = null;
        _activeCursorY = null;
        _isClosing = false;
        DwmHelper.Log("HideFlyout completed: Window hidden");
    }

    /// <summary>
    /// Toggles flyout visibility smoothly with debouncing.
    /// </summary>
    public void ToggleFlyout(double? cursorX = null, double? cursorY = null)
    {
        long now = Environment.TickCount64;
        DwmHelper.Log($"ToggleFlyout entry: now - show = {now - _lastShowTick}, now - hide = {now - _lastHideTick}, Visibility={Visibility}, isClosing={_isClosing}, isOpening={_isOpening}, Cursor=({cursorX},{cursorY})");

        // Protect against rapid hardware switch bounce (< 100ms)
        if (now - _lastShowTick < 100 || now - _lastHideTick < 100)
        {
            DwmHelper.Log("ToggleFlyout: debounced hardware bounce (< 100ms)");
            return;
        }

        // If the flyout is currently visible or actively animating out, the toggle action collapses it
        if (Visibility == Visibility.Visible || _isClosing)
        {
            if (!_isClosing)
            {
                HideFlyout();
            }
            return;
        }

        // If the window recently closed due to losing focus (e.g. user clicked the taskbar button
        // while the window was open, which fired Deactivated just before this TOGGLE command arrived),
        // do not reopen it—the user's click was meant to collapse the open flyout.
        if (now - _lastHideTick < 350)
        {
            DwmHelper.Log($"ToggleFlyout: suppressed reopen because flyout was closed by click just {now - _lastHideTick}ms ago");
            return;
        }

        ShowFlyout(cursorX, cursorY);
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        DwmHelper.Log($"OnStateChanged: WindowState={WindowState}");

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
            HideFlyout();
        }
    }

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (Visibility == Visibility.Visible && e.NewSize.Height >= 100)
        {
            _cachedFlyoutHeight = e.NewSize.Height;
            if (!_isOpening)
            {
                RepositionWindow();
            }
        }
    }

    private void RepositionWindow()
    {
        double width = ActualWidth > 0 ? ActualWidth : Width;
        double height = (ActualHeight >= 100) ? ActualHeight : _cachedFlyoutHeight;

        var pos = DwmHelper.CalculateFlyoutPosition(width, height, _activeCursorX, _activeCursorY, out double dpiScaleX, out double dpiScaleY);
        DwmHelper.Log($"RepositionWindow: W={width}, H={height}, Pos=({pos.X},{pos.Y}), Dpi=({dpiScaleX},{dpiScaleY})");
        Left = pos.X;
        Top = pos.Y;

        var helper = new WindowInteropHelper(this);
        if (helper.Handle != IntPtr.Zero)
        {
            int physX = (int)Math.Round(pos.X * dpiScaleX);
            int physY = (int)Math.Round(pos.Y * dpiScaleY);
            DwmHelper.SetWindowPos(helper.Handle, IntPtr.Zero, physX, physY, 0, 0, DwmHelper.SWP_NOSIZE | DwmHelper.SWP_NOZORDER | DwmHelper.SWP_NOACTIVATE);
        }
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        long diff = Environment.TickCount64 - _lastShowTick;
        DwmHelper.Log($"OnDeactivated: diff={diff}, Visibility={Visibility}, isDialog={_isDialogOpen}, isCtxMenu={_isContextMenuOpen}");

        // Ignore deactivation if folder dialog or context menu is currently open or just closed
        if (_isDialogOpen || _isContextMenuOpen)
        {
            return;
        }

        if (Environment.TickCount64 - _dialogClosedTick < 300)
        {
            return;
        }

        if (Environment.TickCount64 - _contextMenuClosedTick < 300)
        {
            return;
        }

        // Ignore sub-80ms microsecond OS activation handoff jitter
        if (diff < 80)
        {
            return;
        }

        HideFlyout();
    }

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key == Key.Escape)
        {
            if (SettingsPanel.Visibility == Visibility.Visible)
            {
                ShowFavoritesView();
            }
            else
            {
                HideFlyout();
            }
            e.Handled = true;
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        DwmHelper.Log("MainWindow.OnClosing called");
        base.OnClosing(e);
        if (Application.Current is App app)
        {
            app.ExitApplication();
        }
        else
        {
            Application.Current?.Shutdown();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        DwmHelper.Log("MainWindow.OnClosed called");
        base.OnClosed(e);
    }

    // ==========================================
    // ACTION HANDLERS
    // ==========================================

    private void SettingsButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        ShowSettingsView();
    }

    private void BackToFavorites_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        ShowFavoritesView();
    }

    private async void RefreshButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        var rotateAnim = new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(450))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        RefreshIconRotate.BeginAnimation(RotateTransform.AngleProperty, rotateAnim);
        await LoadFoldersAsync(force: true);
    }

    private void ExitButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        HideFlyout();
    }

    private async void AddFolderButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        _isDialogOpen = true;
        try
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Folder to Add to QuickDock",
                Multiselect = true
            };

            if (dialog.ShowDialog(this) == true)
            {
                bool anyAdded = false;
                var folderPaths = (dialog.FolderNames != null && dialog.FolderNames.Length > 0)
                    ? (IEnumerable<string>)dialog.FolderNames
                    : (!string.IsNullOrWhiteSpace(dialog.FolderName) ? new[] { dialog.FolderName } : Array.Empty<string>());

                foreach (string folderPath in folderPaths)
                {
                    if (Directory.Exists(folderPath))
                    {
                        if (FolderConfigService.AddFolder(folderPath))
                        {
                            anyAdded = true;
                        }
                    }
                }
                if (anyAdded)
                {
                    await LoadFoldersAsync(force: true);
                }
            }
        }
        catch (Exception ex)
        {
            DwmHelper.Log($"AddFolder error: {ex.Message}");
        }
        finally
        {
            _isDialogOpen = false;
            _dialogClosedTick = Environment.TickCount64;
            Activate();
            Focus();
        }
    }

    private async void RemoveFolder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is FrameworkElement elem && elem.DataContext is QuickAccessItem item)
        {
            FolderConfigService.RemoveFolder(item.Path);
            await LoadFoldersAsync(force: true);
        }
    }

    private async void MoveUp_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is FrameworkElement elem && elem.DataContext is QuickAccessItem item)
        {
            FolderConfigService.MoveUp(item.Path);
            await LoadFoldersAsync(force: true);
        }
    }

    private async void MoveDown_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is FrameworkElement elem && elem.DataContext is QuickAccessItem item)
        {
            FolderConfigService.MoveDown(item.Path);
            await LoadFoldersAsync(force: true);
        }
    }

    private async void ResetDefaults_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        await FolderConfigService.ResetToQuickAccessDefaultsAsync();
        await LoadFoldersAsync(force: true);
    }

    private void FolderRow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is FrameworkElement elem && elem.DataContext is QuickAccessItem item)
        {
            OpenFolder(item.Path);
            HideFlyout();
        }
    }

    private void TerminalButton_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is FrameworkElement elem && elem.DataContext is QuickAccessItem item)
        {
            OpenTerminal(item.Path);
            HideFlyout();
        }
    }

    // ==========================================
    // CONTEXT MENU HANDLERS
    // ==========================================

    private void ContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        _isContextMenuOpen = true;
    }

    private void ContextMenu_Closed(object sender, RoutedEventArgs e)
    {
        _isContextMenuOpen = false;
        _contextMenuClosedTick = Environment.TickCount64;
    }

    private void ContextMenu_OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is QuickAccessItem item)
        {
            OpenFolder(item.Path);
            HideFlyout();
        }
    }

    private void ContextMenu_OpenTerminal_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is QuickAccessItem item)
        {
            OpenTerminal(item.Path);
            HideFlyout();
        }
    }

    private async void ContextMenu_RemoveFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is QuickAccessItem item)
        {
            FolderConfigService.RemoveFolder(item.Path);
            await LoadFoldersAsync(force: true);
        }
    }

    private void ContextMenu_Settings_Click(object sender, RoutedEventArgs e)
    {
        ShowSettingsView();
    }

    private static void OpenFolder(string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                DwmHelper.Log($"OpenFolder: folder does not exist: {path}");
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{path}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            DwmHelper.Log($"Failed to open folder '{path}': {ex.Message}");
        }
    }

    private static void OpenTerminal(string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                DwmHelper.Log($"OpenTerminal: folder does not exist: {path}");
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "wt.exe",
                Arguments = $"-d \"{path}\"",
                UseShellExecute = true
            });
        }
        catch
        {
            try
            {
                string escapedPath = path.Replace("'", "''");
                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoExit -Command \"Set-Location -LiteralPath '{escapedPath}'\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                DwmHelper.Log($"Failed to open terminal at '{path}': {ex.Message}");
            }
        }
    }
}
