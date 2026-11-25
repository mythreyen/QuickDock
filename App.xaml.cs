using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Shell;
using Microsoft.Win32;
using QuickDock.Services;
using Application = System.Windows.Application;

namespace QuickDock;

public partial class App : Application
{
    private SingleInstanceService? _singleInstanceService;
    private MainWindow? _mainWindow;
    private UserPreferenceChangedEventHandler? _userPreferenceChangedHandler;
    private bool _isPrimaryInstance = false;
    private bool _isExiting = false;

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (s, args) =>
        {
            DwmHelper.Log($"DispatcherUnhandledException: {args.Exception}");
            args.Handled = true; // Prevent unhandled non-fatal UI exception from terminating process
        };
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            DwmHelper.Log($"UnhandledException: {args.ExceptionObject}");
        };

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        base.OnStartup(e);

        // 1. Group window with taskbar shortcut under explicit AppUserModelID
        DwmHelper.SetCurrentProcessExplicitAppUserModelID("QuickDock.App");

        // 2. Parse command-line arguments (JumpList tasks, shortcuts, background mode)
        bool isRefreshArg = false;
        bool isExitArg = false;
        bool isSettingsArg = false;
        bool silent = false;

        foreach (var arg in e.Args)
        {
            if (arg.Equals("--refresh", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("/refresh", StringComparison.OrdinalIgnoreCase))
            {
                isRefreshArg = true;
            }
            else if (arg.Equals("--settings", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("/settings", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("-s", StringComparison.OrdinalIgnoreCase))
            {
                isSettingsArg = true;
            }
            else if (arg.Equals("--exit", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("/exit", StringComparison.OrdinalIgnoreCase))
            {
                isExitArg = true;
            }
            else if (arg.Equals("--background", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("/background", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("-b", StringComparison.OrdinalIgnoreCase))
            {
                silent = true;
            }
        }

        // 3. Single Instance Check via Mutex & IPC
        _singleInstanceService = new SingleInstanceService();
        if (!_singleInstanceService.IsFirstInstance())
        {
            // Another instance is already running! Forward command over Named Pipe and exit.
            if (isExitArg)
            {
                _singleInstanceService.SendCommand("EXIT");
            }
            else if (isRefreshArg)
            {
                _singleInstanceService.SendCommand("REFRESH");
            }
            else if (isSettingsArg)
            {
                _singleInstanceService.SendCommand("SETTINGS");
            }
            else
            {
                _singleInstanceService.NotifyFirstInstance();
            }

            _singleInstanceService.Dispose();
            _singleInstanceService = null;
            DwmHelper.RestoreStandardCursor();
            Environment.Exit(0);
            return;
        }

        _isPrimaryInstance = true;

        // If first instance was launched solely with --exit, exit immediately
        if (isExitArg)
        {
            Shutdown();
            return;
        }

        // 4. Start IPC server to listen for signals from taskbar clicks and JumpList tasks
        _singleInstanceService.CommandReceived += (command) =>
        {
            try
            {
                if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
                    return;

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (command.StartsWith("TOGGLE", StringComparison.OrdinalIgnoreCase))
                        {
                            double? x = null;
                            double? y = null;
                            var parts = command.Split(' ');
                            if (parts.Length >= 3 &&
                                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double px) &&
                                double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double py))
                            {
                                x = px;
                                y = py;
                            }
                            _mainWindow?.ToggleFlyout(x, y);
                        }
                        else if (command.Equals("REFRESH", StringComparison.OrdinalIgnoreCase))
                        {
                            _ = _mainWindow?.LoadFoldersAsync(force: true);
                        }
                        else if (command.Equals("SETTINGS", StringComparison.OrdinalIgnoreCase))
                        {
                            _mainWindow?.OpenSettingsDirectly();
                        }
                        else if (command.Equals("EXIT", StringComparison.OrdinalIgnoreCase))
                        {
                            ExitApplication();
                        }
                    }
                    catch (Exception ex)
                    {
                        DwmHelper.Log($"Error handling IPC command: {ex.Message}");
                    }
                }));
            }
            catch (Exception ex)
            {
                DwmHelper.Log($"Error dispatching IPC command to UI thread: {ex.Message}");
            }
        };
        _singleInstanceService.StartIpcServer();

        DwmHelper.Log("OnStartup step 1: IPC server ready");

        // 5. Initialize MainWindow
        _mainWindow = new MainWindow();
        DwmHelper.Log("OnStartup step 2: MainWindow instantiated");

        // 6. Setup Taskbar JumpList (native right-click options)
        SetupJumpList();

        // 7. Listen to Windows theme changes (Light/Dark mode)
        _userPreferenceChangedHandler = (s, args) =>
        {
            if (args.Category == UserPreferenceCategory.General || args.Category == UserPreferenceCategory.Color)
            {
                try
                {
                    if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
                        return;

                    Dispatcher.Invoke(() =>
                    {
                        _mainWindow?.ApplyTheme(DwmHelper.IsDarkModeEnabled());
                    });
                }
                catch {}
            }
        };
        SystemEvents.UserPreferenceChanged += _userPreferenceChangedHandler;

        // 8. Preload folders in background so subsequent opens are instant
        _ = _mainWindow.LoadFoldersAsync();

        if (isSettingsArg)
        {
            DwmHelper.GetCursorPos(out var pt);
            _mainWindow.OpenSettingsDirectly(pt.X, pt.Y);
            DwmHelper.Log($"OnStartup completed: First instance opened directly in settings at ({pt.X}, {pt.Y})");
        }
        else if (!silent)
        {
            DwmHelper.GetCursorPos(out var pt);
            _mainWindow.ShowFlyout(pt.X, pt.Y);
            DwmHelper.Log($"OnStartup completed: First instance shown at ({pt.X}, {pt.Y})");
        }
        else
        {
            _mainWindow.Hide();
            DwmHelper.Log("OnStartup completed: Started hidden (pinned taskbar launcher ready)");
        }
    }

    private void SetupJumpList()
    {
        try
        {
            string exePath = Environment.ProcessPath ?? "";
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                return;

            var jumpList = new JumpList();
            JumpList.SetJumpList(this, jumpList);

            var settingsTask = new JumpTask
            {
                Title = "Configure Folders",
                Description = "Add or remove folders from QuickDock",
                ApplicationPath = exePath,
                Arguments = "--settings",
                IconResourcePath = exePath,
                IconResourceIndex = 0
            };

            var refreshTask = new JumpTask
            {
                Title = "Refresh Folders",
                Description = "Reload folders from configuration",
                ApplicationPath = exePath,
                Arguments = "--refresh",
                IconResourcePath = exePath,
                IconResourceIndex = 0
            };

            var exitTask = new JumpTask
            {
                Title = "Exit QuickDock",
                Description = "Close and exit QuickDock",
                ApplicationPath = exePath,
                Arguments = "--exit",
                IconResourcePath = exePath,
                IconResourceIndex = 0
            };

            jumpList.JumpItems.Add(settingsTask);
            jumpList.JumpItems.Add(refreshTask);
            jumpList.JumpItems.Add(exitTask);
            jumpList.Apply();
        }
        catch (Exception ex)
        {
            DwmHelper.Log($"Failed to configure JumpList: {ex.Message}");
        }
    }

    public void ExitApplication()
    {
        if (_isExiting) return;
        _isExiting = true;

        DwmHelper.Log("App.ExitApplication called");

        if (_userPreferenceChangedHandler != null)
        {
            try
            {
                SystemEvents.UserPreferenceChanged -= _userPreferenceChangedHandler;
                _userPreferenceChangedHandler = null;
            }
            catch {}
        }

        try
        {
            _singleInstanceService?.Dispose();
            _singleInstanceService = null;
        }
        catch {}

        try
        {
            var win = _mainWindow;
            _mainWindow = null;
            win?.Close();
        }
        catch {}

        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_isPrimaryInstance)
        {
            DwmHelper.Log($"Primary App.OnExit called with code {e.ApplicationExitCode}");
            ExitApplication();
        }
        else
        {
            DwmHelper.Log($"Secondary instance exited with code {e.ApplicationExitCode}");
        }
        base.OnExit(e);
    }
}
