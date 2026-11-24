# QuickDock — Airy Windows 11 Taskbar Folder Flyout

**QuickDock** is a lightweight, ultra-clean Windows 11 desktop utility built in **C# (.NET 8, WPF)** that provides instant, buttery-smooth access to your pinned **Quick Access** folders directly from the Windows taskbar.

---

## ✨ Features & Visual Design

* **Windows 11 Native Transient Acrylic Backdrop:**
  * Uses native Win32 `DwmSetWindowAttribute` (`DWMWA_SYSTEMBACKDROP_TYPE = 3` / `DWMSBT_TRANSIENTWINDOW`) and rounded corners (`DWMWA_WINDOW_CORNER_PREFERENCE = 2` / `DWMWCP_ROUND`).
  * Delicate `1px` inner border (`#22FFFFFF` in dark mode, `#18000000` in light mode) for a crisp floating edge.
  * Dynamically adapts to Windows Light/Dark theme changes in real-time.
* **Airy UI Language & Whitespace:**
  * Frameless, borderless floating window (`WindowStyle="None"`, `AllowsTransparency="False"`).
  * Whisper-quiet uppercase header label (`FAVORITES`, 10px, 50% opacity, generous padding).
  * Fixed width of `288px`, dynamic auto-sizing height capped at `440px`.
  * Auto-hiding modern thin scrollbar when folders exceed height.
* **Spacious Folder Rows:**
  * `42px` row height with `4px` vertical spacing and `8px` corner radius.
  * `20x20` native Windows folder shell icon extracted via `SHGetFileInfo`.
  * Folder name in **Segoe UI Variable Text** (`13.5px`, SemiBold/Regular) with clean ellipsis truncation.
  * Full path hidden from visual noise, displayed on subtle hover tooltip.
  * Smooth `120ms` hover transition with soft translucent pill highlight (`#14FFFFFF` dark / `#0A000000` light).
  * Faint right-aligned **Windows Terminal** action button on row hover (`wt.exe -d "<path>"`).
* **Smooth Physics & Auto-Dismiss:**
  * **Entrance:** `180ms` upward slide (`TranslateY` from `+10px` to `0`) paired with a `0 → 1` opacity fade using an ease-out cubic curve.
  * **Exit:** Crisp `100ms` fade-out when dismissed.
  * Auto-dismisses when the flyout loses focus (`Deactivated`), when pressing `Esc`, or upon launching a folder/terminal.
* **Pure Taskbar Integration (No System Tray):**
  * **Taskbar Resident:** Operates exclusively from the Windows taskbar with `ShowInTaskbar="True"` and native `AppUserModelID` grouping (`QuickDock.App`).
  * **Smooth Minimize/Restore:** When deactivated or pressing `Esc`, the flyout smoothly minimizes to the taskbar. Clicking the taskbar icon restores and summons it.
  * **Taskbar JumpList:** Right-clicking the taskbar icon provides native options: *Refresh Folders*, *Exit QuickDock*, and *Close window*.
  * **Header Controls:** Discrete inline action buttons in the flyout header for instant folder refresh (`⟳`) and closing (`✕`).
  * **Pinned Shortcut IPC:** Named `Mutex` + `NamedPipeServerStream` single-instance IPC architecture. When the pinned `.exe` is clicked from the taskbar or launched again, the secondary instance signals the primary instance to toggle visibility and exits in ~10ms.
* **Smart Multi-Monitor DPI Positioning:**
  * Automatically calculates the cursor position (`GetCursorPos`), identifies the current monitor, scales with per-monitor DPI, centers horizontally above the click, rests `12px` above the taskbar work area, and clamps cleanly within screen bounds.

---

## 🏗️ Architecture

```
QuickDock/
├── QuickDock.csproj                # .NET 8 WPF, Per-Monitor V2 DPI, Assets/app.ico
├── app.manifest                    # Windows 10/11 OS compatibility manifest
├── App.xaml / App.xaml.cs          # Taskbar JumpList, single-instance mutex, named pipe IPC
├── MainWindow.xaml / .cs           # Frameless acrylic flyout, header controls, animations, auto-dismiss
├── Assets/
│   └── app.ico                     # Embedded multi-resolution application & taskbar icon
├── Models/
│   └── QuickAccessItem.cs          # Item model (Name, Path, Icon)
└── Services/
    ├── DwmHelper.cs                # Win32 P/Invoke (Acrylic, rounded corners, theme, monitor DPI, AppUserModelID)
    ├── QuickAccessService.cs       # COM Shell.Application Quick Access reader (STA async)
    └── SingleInstanceService.cs    # Named Mutex + NamedPipe IPC server & client
```

---

## 🚀 Building & Running

### Prerequisites
* Windows 11 (or Windows 10 Build 19041+)
* [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or higher

### Build
```powershell
dotnet build -c Release
```

### Run
```powershell
dotnet run
```
Or launch the compiled binary:
```powershell
.\bin\Release\net8.0-windows\QuickDock.exe
```

### Pinning to Taskbar
1. Build in Release mode: `dotnet build -c Release`.
2. Locate `bin\Release\net8.0-windows\QuickDock.exe`.
3. Right-click `QuickDock.exe` and select **Pin to taskbar**.
4. Clicking the pinned taskbar icon smoothly toggles the QuickDock flyout!
