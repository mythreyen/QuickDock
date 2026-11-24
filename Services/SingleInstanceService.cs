using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace QuickDock.Services;

public sealed class SingleInstanceService : IDisposable
{
    private const string MutexName = @"Local\QuickDock_App_SingleInstance_Mutex_98a7c2";
    private const string PipeName = "QuickDock_App_IPC_Pipe_98a7c2";

    private static Mutex? _mutex;
    private bool _hasMutex;
    private CancellationTokenSource? _cts;

    public event Action<double?, double?>? ToggleRequested;
    public event Action<string>? CommandReceived;

    /// <summary>
    /// Checks if this is the first instance of QuickDock.
    /// Returns true if first; false if an instance is already running.
    /// Safely recovers from abandoned mutexes if previous process terminated abruptly.
    /// </summary>
    public bool IsFirstInstance()
    {
        try
        {
            _mutex = new Mutex(true, MutexName, out _hasMutex);
            return _hasMutex;
        }
        catch (AbandonedMutexException)
        {
            // The previous owner exited without releasing the mutex; this instance now owns it
            _hasMutex = true;
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error acquiring Mutex: {ex.Message}");
            return false;
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);
    private const int ASFW_ANY = -1;

    /// <summary>
    /// Sends a raw command line string to the primary running instance over Named Pipe.
    /// </summary>
    public void SendCommand(string command)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                AllowSetForegroundWindow(ASFW_ANY);
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                client.Connect(1500);
                using var writer = new StreamWriter(client) { AutoFlush = true };
                writer.WriteLine(command);
                writer.Flush();
                client.WaitForPipeDrain();
                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to send command '{command}' (attempt {attempt + 1}): {ex.Message}");
                if (attempt == 0)
                {
                    Thread.Sleep(100);
                }
            }
        }
    }

    /// <summary>
    /// Sends a TOGGLE command to the already running primary instance over the Named Pipe.
    /// </summary>
    public void NotifyFirstInstance()
    {
        DwmHelper.GetCursorPos(out var pt);
        SendCommand($"TOGGLE {pt.X.ToString(CultureInfo.InvariantCulture)} {pt.Y.ToString(CultureInfo.InvariantCulture)}");
    }

    /// <summary>
    /// Starts the background IPC Named Pipe server to listen for signals from subsequent instances.
    /// </summary>
    public void StartIpcServer()
    {
        _cts = new CancellationTokenSource();
        Task.Run(() => ServerLoop(_cts.Token));
    }

    private async Task ServerLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(token);

                using var reader = new StreamReader(server);
                string? command = await reader.ReadLineAsync(token);
                if (!string.IsNullOrEmpty(command))
                {
                    DispatchCommand(command);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"IPC Server connection error: {ex.Message}");
                try
                {
                    await Task.Delay(200, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private void DispatchCommand(string command)
    {
        try
        {
            CommandReceived?.Invoke(command);

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
                ToggleRequested?.Invoke(x, y);
            }
        }
        catch (Exception ex)
        {
            DwmHelper.Log($"Error dispatching IPC command '{command}': {ex.Message}");
        }
    }

    public void Dispose()
    {
        try
        {
            _cts?.Cancel();
        }
        catch {}

        if (_hasMutex && _mutex != null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch {}
            try
            {
                _mutex.Dispose();
            }
            catch {}
            _hasMutex = false;
        }
    }
}
