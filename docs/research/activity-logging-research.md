# Activity Logging Research Spike

> **Date:** 2026-03-19
> **Author:** Nova (Tech Lead)
> **Branch:** `feature/activity-logging-research`
> **Status:** Research — no code changes

## Context

Windows Screen Logger currently captures periodic screenshots on a timer (default 5 s) and saves them as JPEG/PNG/WebP files in date-based folders (`{rootPath}/yyyy-MM-dd/screenshot_HHmmss.jpg`). The app runs as a WinForms system-tray application targeting **.NET 9 on Windows 11** (`net9.0-windows10.0.22000.0`).

The goal is to capture **structured user-activity metadata** alongside screenshots — enough context for an AI to later produce a meaningful daily work summary. The summarisation engine itself is out of scope; this repo is concerned only with **data capture**.

### Existing Architecture (Relevant)

| Component | Role |
|---|---|
| `MainForm` | WinForms host. Owns a `System.Windows.Forms.Timer` (`captureTimer`) that fires `CaptureTimer_Tick`. Pauses capture on session lock / system suspend. |
| `ScreenshotService` | Injected with `AppConfiguration` + `ILogger`. `CaptureAllScreens()` grabs the virtual screen via GDI+, encodes with SkiaSharp, writes to `{rootPath}/yyyy-MM-dd/screenshot_HHmmss.{ext}`. |
| `CleanupService` | Deletes date-folders older than `ClearDays`. |
| `AppConfiguration` | JSON-serialised settings (`config.json` in `%AppData%`). Loaded once, hot-reloaded on settings dialog save. |
| `ILogger` / `DefaultLogger` | Simple file logger with level filtering. |
| **NuGet packages** | `SkiaSharp 3.119.0`, `System.CommandLine 2.0.0-beta4`, `System.Text.Json 9.0.8`, `Microsoft.Windows.ImplementationLibrary 1.0.250325.1` |

### Key Constraints

- **Minimum performance impact.** The app must never make the user's machine feel sluggish.
- **No network calls.** All data stays local.
- **Self-contained single-file deployment** (`PublishSingleFile`, `SelfContained`, `win-x64`). Any new dependency ships inside the EXE.
- **Data format**: compact, AI-readable, not bloated — suitable for feeding into an LLM context window.

---

## 1. Signal Inventory

### 1.1 Active Window / Process Tracking

| Aspect | Detail |
|---|---|
| **Data provided** | Process name, PID, executable path, main window title, window class name |
| **API** | Win32: `GetForegroundWindow` → `GetWindowThreadProcessId` → `System.Diagnostics.Process.GetProcessById(pid)`. Window title: `GetWindowText` (P/Invoke) or `Process.MainWindowTitle`. Executable path: `Process.MainModule.FileName` (may require elevation for system processes). |
| **P/Invoke signatures** | See §4 for exact declarations. |
| **NuGet packages** | None — all built-in (`System.Diagnostics.Process`, `System.Runtime.InteropServices`). |
| **Performance cost** | Negligible. `GetForegroundWindow` + `GetWindowText` is a pair of user32.dll calls completing in <0.1 ms. `Process.GetProcessById` allocates but is cheap when called once per interval. |
| **Privacy surface** | **Medium.** Window titles frequently contain document names, URLs, email subjects, chat message previews. Process names reveal which applications the user runs. |
| **Reliability** | **High.** Works for all Win32 and WPF apps. UWP/MSIX apps return the container process (`ApplicationFrameHost.exe`) — the real process name requires `UWP_GetCurrentPackageFullName` or enumerating child windows. See §4 for workaround. Elevated processes may refuse `MainModule.FileName` from a non-elevated caller. |
| **AI usefulness** | **Very high.** Window titles are the single richest low-cost signal. "JIRA-4532 — Sprint Board — Google Chrome" immediately tells an AI the user is doing project management on ticket JIRA-4532. |

### 1.2 Windows UI Automation

| Aspect | Detail |
|---|---|
| **Data provided** | Focused element text, control type, document title, URL bar content (browser), selected text, value patterns. Can walk the full accessibility tree of any automation-aware application. |
| **API** | `System.Windows.Automation` (managed UIA wrapper in `UIAutomationClient.dll` / `UIAutomationTypes.dll`). Alternatively, COM-based `IUIAutomation` via `Interop.UIAutomationClient`. |
| **NuGet packages** | None for the managed API — it ships with the Windows SDK and is available via FrameworkReference. For COM: `Interop.UIAutomationClient` NuGet or hand-rolled COM interop. |
| **Performance cost** | **Medium-High.** Cross-process COM calls. Querying the focused element is fast (~2-5 ms). Walking an entire tree can take 50-500 ms+ depending on app complexity. Querying on every 5 s tick is feasible if scoped to the focused element only. |
| **Privacy surface** | **Very high.** Can read arbitrary text content from any accessible control — passwords in poorly-masked fields, private messages, financial data. |
| **Reliability** | **Medium.** Works well for Win32, WPF, WinForms, and modern browsers (Chrome, Edge expose accessibility). Fails or returns empty for: Java apps without the Java Access Bridge, some Electron apps with accessibility disabled, games, custom-rendered UI. COM exceptions (`ElementNotAvailableException`) are common when windows close between query and read. |
| **AI usefulness** | **High** when it works — e.g., reading a browser's address bar gives a full URL. But the inconsistency and performance cost reduce the cost-effectiveness. |

### 1.3 OCR (Optical Character Recognition)

| Aspect | Detail |
|---|---|
| **Data provided** | All visible text on screen, with bounding boxes and confidence scores. |
| **API options** | (a) `Windows.Media.Ocr.OcrEngine` (built-in WinRT, ships with Windows 10+). (b) Tesseract (open-source, NuGet `Tesseract` v5.x). See §6 for detailed assessment. |
| **NuGet packages** | WinRT OCR: none (part of Windows, accessed via WinRT projection). Tesseract: `Tesseract` NuGet + `tessdata` language models (~15 MB for English). |
| **Performance cost** | **High.** WinRT OCR on a 1920×1080 image: ~200-600 ms. Tesseract on same: ~500-2000 ms. Both are CPU-intensive. Running on every 5 s tick is too aggressive; every 30-60 s is feasible on a background thread. |
| **Privacy surface** | **Extreme.** OCR captures everything visible — passwords, credit card numbers, private messages, medical records. Requires aggressive redaction. |
| **Reliability** | **High** for text-heavy screens (documents, browsers, IDEs). Poor for: small/stylised fonts, overlapping UI, dark themes with low contrast (some preprocessing helps), non-Latin scripts (need additional tessdata). |
| **AI usefulness** | **Very high** in theory — full text context. But the volume is enormous (thousands of words per frame), which bloats the activity log and makes AI summarisation harder unless intelligently filtered/cropped. |

### 1.4 Clipboard Monitoring

| Aspect | Detail |
|---|---|
| **Data provided** | Text content copied to clipboard, timestamp of copy event. Can also detect image/file copy events (but text is the useful signal). |
| **API** | `AddClipboardFormatListener` (Win32, P/Invoke) to receive `WM_CLIPBOARDUPDATE` messages. Read text: `Clipboard.GetText()` (WinForms) or `System.Windows.Forms.Clipboard.ContainsText()`. Must be called from a thread with a message pump (the UI thread, or a dedicated STA thread). |
| **NuGet packages** | None — built-in. |
| **Performance cost** | **Negligible.** Event-driven — zero CPU until a clipboard change occurs. Reading text content is instant. |
| **Privacy surface** | **Very high.** Users copy passwords, credit card numbers, private URLs, confidential text. Clipboard is one of the most sensitive data sources. |
| **Reliability** | **High** for text. The `WM_CLIPBOARDUPDATE` mechanism is the modern recommended approach (replaces the old clipboard chain). Works universally. Only caveat: some password managers clear the clipboard after a timeout, generating spurious events. |
| **AI usefulness** | **Medium.** Clipboard text is a sporadic signal — useful when it contains a ticket number, URL, or customer name the user is working with, but many clipboard operations are noise (copying code snippets, formatting). Best used as a supplementary signal, not primary. |

### 1.5 Input Activity (Keyboard / Mouse Presence)

| Aspect | Detail |
|---|---|
| **Data provided** | Binary "user is active" signal: was there keyboard/mouse input in the last N seconds? Optionally: keystrokes-per-minute and mouse-distance-per-minute as productivity intensity metrics. **Never log actual keystrokes.** |
| **API** | `GetLastInputInfo` (Win32, P/Invoke) returns tick count of last input event. Compare with `Environment.TickCount` to get idle duration. No hooks needed — this is a simple polling API. |
| **P/Invoke signature** | See §4. |
| **NuGet packages** | None. |
| **Performance cost** | **Negligible.** Single kernel call, <0.01 ms. |
| **Privacy surface** | **Low** when limited to idle detection. If keystroke/mouse counts are added, still low — no content is captured. |
| **Reliability** | **Very high.** Works universally, no edge cases. |
| **AI usefulness** | **Medium.** Distinguishes "user was working" from "user was away." Helps an AI ignore idle periods and correctly summarise working hours. Not useful for understanding *what* the user was doing. |

### 1.6 Browser URL Extraction (via UI Automation)

| Aspect | Detail |
|---|---|
| **Data provided** | The URL currently displayed in the browser's address bar. |
| **API** | UI Automation: find the address bar element in Chrome/Edge/Firefox by `ControlType.Edit` + `AutomationId` or `Name` pattern, then read its `Value` pattern. |
| **Performance cost** | **Low-Medium.** ~5-15 ms per query if the browser is the foreground window. |
| **Privacy surface** | **High.** URLs contain search queries, internal tool paths, banking sites, etc. |
| **Reliability** | **Medium.** Chrome and Edge expose the address bar reliably. Firefox's accessibility varies by version. Private/Incognito mode still exposes the URL via UIA. |
| **AI usefulness** | **Very high.** A URL like `https://jira.company.com/browse/PROJ-1234` is directly parseable by an AI into project + ticket context. |

### 1.7 File System Watcher (Recently Modified Files)

| Aspect | Detail |
|---|---|
| **Data provided** | Paths of files being written/modified in watched directories (e.g., user's code workspace). |
| **API** | `System.IO.FileSystemWatcher`. |
| **Performance cost** | **Low** for scoped directories. High/unreliable for broad watches (entire drive). |
| **Privacy surface** | **Medium.** File paths reveal project names and document titles. |
| **Reliability** | **Medium.** Buffer overflows under heavy I/O, doesn't work on network drives reliably. |
| **AI usefulness** | **Medium.** Knowing the user edited `src/auth/login.ts` tells an AI about the project context. But this requires the user to configure watched directories. |

---

## 2. Data Schema Design

### 2.1 Activity Record (JSON Lines format)

Each capture interval produces one JSON record, appended to a `.jsonl` (JSON Lines) file.

```jsonc
{
  "ts": "2026-03-19T14:23:05.123Z",       // ISO 8601 UTC timestamp
  "proc": "chrome",                         // Process name (lowercase, no .exe)
  "pid": 12840,                             // Process ID
  "title": "JIRA-4532 - Sprint Board - Google Chrome", // Window title
  "exe": "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe", // Executable path (optional, omit if same as previous)
  "idle": 2,                                // Seconds since last user input
  "cat": "browser",                         // Category hint (see below)
  "ctx": {                                  // Extracted context (optional, populated by Phase 2+)
    "url": "https://jira.company.com/browse/JIRA-4532",
    "clip": "Customer: Acme Corp"           // Last clipboard text (if recently changed)
  },
  "screen": "screenshot_142305.jpg"         // Filename of associated screenshot (same folder)
}
```

### 2.2 Size Analysis

| Field | Typical bytes |
|---|---|
| `ts` | 30 |
| `proc` + `pid` | 20 |
| `title` | 60-120 |
| `exe` | 0 (omit when unchanged) to 80 |
| `idle` | 5 |
| `cat` | 15 |
| `ctx` | 0-100 |
| `screen` | 30 |
| JSON overhead | ~40 |

**Typical record size: 200-350 bytes.** Well under the 500-byte target.

At a 5-second interval, that's ~17,280 records/day × 300 bytes ≈ **5 MB/day** of activity data. At 30-second intervals (more realistic for activity logging): ~2,880 records/day ≈ **0.8 MB/day**.

### 2.3 Category Hints

Pre-classified categories to reduce AI work:

| Category | Example processes |
|---|---|
| `browser` | chrome, msedge, firefox, brave |
| `ide` | devenv, code, rider, idea64 |
| `terminal` | WindowsTerminal, cmd, powershell, pwsh |
| `office` | WINWORD, EXCEL, POWERPNT, OUTLOOK |
| `communication` | Teams, Slack, Discord, Zoom |
| `design` | figma, Photoshop, Illustrator |
| `media` | Spotify, vlc, Netflix (browser tab) |
| `system` | explorer, taskmgr, mmc |
| `unknown` | Anything not mapped |

Category mapping is a simple dictionary lookup on process name. It's cheap, deterministic, and immediately useful for AI summarisation.

### 2.4 File Layout

```
WindowsScreenLogger/
└── 2026-03-19/
    ├── screenshot_142305.jpg
    ├── screenshot_142310.jpg
    ├── screenshot_142315.jpg
    └── activity.jsonl          ← NEW: one line per capture interval
```

The `activity.jsonl` file lives in the same date folder as its screenshots. Benefits:
- `CleanupService` already deletes entire date folders — activity data is cleaned up automatically.
- AI summarisation can be scoped to a single day by reading one `.jsonl` file.
- Each record's `screen` field links to the screenshot taken at the same time.

---

## 3. Signal Ranking

### Ranking Formula

**Score = (AI value) ÷ (Implementation complexity + Performance cost)**

Each factor rated 1-5 (5 = highest value or highest cost).

| # | Signal | AI Value | Impl. Complexity | Perf. Cost | Score | Phase |
|---|--------|----------|------------------|------------|-------|-------|
| 1 | Active window / process name | 5 | 1 | 1 | **2.50** | **Phase 1** |
| 2 | Window title | 5 | 1 | 1 | **2.50** | **Phase 1** |
| 3 | Input idle detection | 3 | 1 | 1 | **1.50** | **Phase 1** |
| 4 | Process category hint | 4 | 1 | 1 | **2.00** | **Phase 1** |
| 5 | Browser URL (via UIA) | 5 | 3 | 2 | **1.00** | **Phase 2** |
| 6 | Clipboard text | 3 | 2 | 1 | **1.00** | **Phase 2** |
| 7 | OCR (full screen) | 5 | 4 | 5 | **0.56** | **Phase 3** |
| 8 | UI Automation (focused element) | 4 | 4 | 3 | **0.57** | **Phase 3** |
| 9 | File system watcher | 3 | 3 | 2 | **0.60** | **Phase 3** |

### Phased Implementation Plan

#### Phase 1 — Quick Wins (1-2 days)
- **Active window tracking**: process name, PID, window title, executable path
- **Idle detection**: seconds since last input via `GetLastInputInfo`
- **Category classification**: dictionary-based process→category mapping
- **JSON Lines output**: one record per capture tick, written to `activity.jsonl`
- **Configuration**: `enableActivityLogging` flag in `AppConfiguration`

*Delivers ~80% of the AI summarisation value with minimal effort.*

#### Phase 2 — Richer Context (2-3 days)
- **Browser URL extraction**: UI Automation query for Chrome/Edge address bar
- **Clipboard monitoring**: event-driven, captures text when clipboard changes
- **UWP process resolution**: resolve `ApplicationFrameHost.exe` to real app name
- **Deduplication**: skip writing a record if process + title unchanged since last tick

*Adds URL and clipboard context — the signals that let an AI identify specific tickets, customers, and projects.*

#### Phase 3 — Optional / Heavy (research-dependent)
- **OCR on screenshots**: extract text from already-captured screenshot images
- **UI Automation tree walk**: deeper element extraction for non-browser apps
- **File system watcher**: track recently modified source files
- **Smart cropping**: OCR only the title bar / focused area instead of full screen

*High value but high cost. Only pursue if Phase 1+2 don't provide enough context for quality summaries.*

---

## 4. Windows API Deep-Dive

### 4.1 GetForegroundWindow + GetWindowText (Phase 1)

```csharp
using System.Runtime.InteropServices;
using System.Text;

internal static partial class NativeMethods
{
    [LibraryImport("user32.dll")]
    internal static partial IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern int GetWindowTextLength(IntPtr hWnd);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
```

**Usage:**
```csharp
var hwnd = NativeMethods.GetForegroundWindow();
NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);

// Window title
int length = NativeMethods.GetWindowTextLength(hwnd);
var sb = new StringBuilder(length + 1);
NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
string windowTitle = sb.ToString();

// Process info
using var process = Process.GetProcessById((int)pid);
string processName = process.ProcessName;
string? exePath = null;
try { exePath = process.MainModule?.FileName; }
catch (System.ComponentModel.Win32Exception) { /* elevated process, access denied */ }
```

**Threading:** All these calls are safe from any thread. No COM apartment requirements.

**Known limitations:**
- **UWP apps:** `GetForegroundWindow` returns `ApplicationFrameHost.exe`. Workaround: enumerate child windows of the frame host with `EnumChildWindows` and find the one owned by a different process. See §4.4.
- **Elevated processes:** `Process.MainModule.FileName` throws `Win32Exception` when the target process is elevated and the caller is not. The process name is still available via `Process.ProcessName`. Mitigate by catching the exception and falling back.
- **Full-screen games / DirectX apps:** Usually work fine — `GetForegroundWindow` still returns the game window. Title may be less informative ("Game Window" vs. a meaningful title).

### 4.2 GetLastInputInfo (Phase 1)

```csharp
[StructLayout(LayoutKind.Sequential)]
internal struct LASTINPUTINFO
{
    public uint cbSize;
    public uint dwTime;
}

internal static partial class NativeMethods
{
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetLastInputInfo(ref LASTINPUTINFO plii);
}
```

**Usage:**
```csharp
static uint GetIdleSeconds()
{
    var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
    if (!NativeMethods.GetLastInputInfo(ref info))
        return 0;
    return (uint)((Environment.TickCount - (int)info.dwTime) / 1000);
}
```

**Threading:** Safe from any thread. No COM requirements.

**Known limitations:** `Environment.TickCount` wraps after ~49 days of uptime. Use `Environment.TickCount64` on .NET 9 and compute the delta with `(uint)(Environment.TickCount64 & 0xFFFFFFFF)` to match the `LASTINPUTINFO.dwTime` (which is 32-bit). In practice, the idle duration is always short so wrapping is not a real issue.

### 4.3 Clipboard Monitoring (Phase 2)

```csharp
internal static partial class NativeMethods
{
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool AddClipboardFormatListener(IntPtr hwnd);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RemoveClipboardFormatListener(IntPtr hwnd);

    internal const int WM_CLIPBOARDUPDATE = 0x031D;
}
```

**Usage:** Override `WndProc` in a Form (or `NativeWindow`) to receive `WM_CLIPBOARDUPDATE`:
```csharp
protected override void WndProc(ref Message m)
{
    if (m.Msg == NativeMethods.WM_CLIPBOARDUPDATE)
    {
        if (Clipboard.ContainsText())
        {
            string text = Clipboard.GetText();
            // Store for next activity record
        }
    }
    base.WndProc(ref m);
}
```

**Threading:** Clipboard operations must occur on the STA thread that owns the window. Since `MainForm` is already a WinForms form on the UI thread, this integrates naturally.

**Known limitations:**
- Must call `AddClipboardFormatListener(this.Handle)` after the handle is created.
- Must call `RemoveClipboardFormatListener` on dispose.
- Some apps (password managers) rapidly set and clear the clipboard — debounce or filter short-lived clipboard content.

### 4.4 UWP Process Resolution (Phase 2)

```csharp
// For ApplicationFrameHost windows, find the real child window
[DllImport("user32.dll")]
[return: MarshalAs(UnmanagedType.Bool)]
static extern bool EnumChildWindows(IntPtr hwndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

static uint GetRealProcessId(IntPtr frameHostHwnd)
{
    uint realPid = 0;
    EnumChildWindows(frameHostHwnd, (childHwnd, _) =>
    {
        NativeMethods.GetWindowThreadProcessId(childHwnd, out uint childPid);
        NativeMethods.GetWindowThreadProcessId(frameHostHwnd, out uint parentPid);
        if (childPid != parentPid)
        {
            realPid = childPid;
            return false; // stop enumerating
        }
        return true;
    }, IntPtr.Zero);
    return realPid;
}
```

**Usage:** When `Process.GetProcessById(pid).ProcessName == "ApplicationFrameHost"`, call `GetRealProcessId(hwnd)` and use that PID instead.

### 4.5 Browser URL via UI Automation (Phase 2)

```csharp
using System.Windows.Automation;

static string? GetBrowserUrl(IntPtr browserHwnd)
{
    try
    {
        var element = AutomationElement.FromHandle(browserHwnd);

        // Chrome/Edge: address bar has ControlType.Edit and Name containing "Address"
        var condition = new AndCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
            new PropertyCondition(AutomationElement.NameProperty, "Address and search bar",
                PropertyConditionFlags.IgnoreCase));

        var addressBar = element.FindFirst(TreeScope.Descendants, condition);

        if (addressBar?.GetCurrentPattern(ValuePattern.Pattern) is ValuePattern vp)
            return vp.Current.Value;
    }
    catch (ElementNotAvailableException)
    {
        // Window closed between query and read
    }
    return null;
}
```

**Threading:** UI Automation COM objects are apartment-threaded. The recommended approach is to call from an STA thread. Using `Task.Run` (which uses the thread pool / MTA) can cause `InvalidOperationException`. Options:
1. Call from the UI thread (safe but blocks briefly).
2. Create a dedicated STA background thread for UIA queries.

**Known limitations:**
- The `Name` property for the address bar differs across browsers:
  - **Chrome/Edge:** `"Address and search bar"`
  - **Firefox:** `"Search with Google or enter address"` (varies by locale and version)
- If the address bar isn't focused or visible (e.g., full-screen mode), the element may still be found but could return stale URLs.
- **Performance:** `FindFirst(TreeScope.Descendants, ...)` on a browser window with many tabs can take 10-50 ms. Acceptable at 5-30 s intervals.

### 4.6 Existing .NET Wrappers

| Library | Description | Recommendation |
|---|---|---|
| `FlaUI` (NuGet: FlaUI.Core + FlaUI.UIA3) | Modern .NET wrapper around COM UIA3. Actively maintained. | **Recommended** if Phase 2 UIA work is pursued. Handles COM lifetime and threading. |
| `System.Windows.Automation` | Built-in managed UIA wrapper. Older API (UIA2 patterns). | Sufficient for basic focused-element and URL extraction. No extra dependency. |
| `Vanara.PInvoke` (NuGet) | Comprehensive P/Invoke wrapper for all Win32 APIs. | Overkill for our needs — we only need 5-6 P/Invoke declarations. |
| `H.InputSimulator` / `InputSimulatorStandard` | Input simulation. | Not needed — we only read state, not simulate input. |

**Recommendation:** Use raw P/Invoke for Phase 1 (5-6 simple declarations). Consider `FlaUI` for Phase 2 if UI Automation complexity grows beyond the address-bar query.

---

## 5. UI Automation Assessment

### 5.1 What Can It Reliably Extract?

| Data Point | Reliability | Notes |
|---|---|---|
| Focused element text | **High** | `AutomationElement.FocusedElement.Current.Name` — works for most controls. |
| Document title | **High** | Usually available as `AutomationElement.FromHandle(hwnd).Current.Name`. |
| Browser URL | **Medium-High** | Works for Chrome, Edge. Firefox varies. See §4.5. |
| Selected/highlighted text | **Low** | `TextPattern.GetSelection()` works in some apps (Notepad, Word) but not most. |
| Specific control values | **Medium** | `ValuePattern`, `RangeValuePattern` available if the app implements them. |
| Full document content | **Low** | `TextPattern.DocumentRange.GetText(-1)` only works in apps with full TextPattern support (Word, some RichEdit controls). |

### 5.2 Performance

- **`AutomationElement.FocusedElement`**: 1-3 ms. Safe to call every 5 seconds.
- **`FromHandle(hwnd)` + property read**: 1-5 ms. Safe at any reasonable interval.
- **`FindFirst(TreeScope.Descendants, ...)`**: 10-200 ms depending on tree complexity. Safe at 30+ second intervals.
- **`FindAll` / full tree walk**: 50-1000+ ms. **Not safe** at short intervals. Only use on-demand or at 60+ second intervals.

**Recommendation:** Scope UIA queries to the focused element and one specific element lookup (e.g., address bar) per tick. Never walk the full tree on a timer.

### 5.3 Failure Modes

| Failure | Frequency | Mitigation |
|---|---|---|
| `ElementNotAvailableException` | Common | Window closed or element destroyed between query and read. Wrap in try/catch, return null. |
| `COMException` (various HRESULTs) | Occasional | UIA server in the target process crashed or is unresponsive. Catch broadly, log, continue. |
| Timeout / hang | Rare | Some apps (notably Java without Access Bridge) can cause UIA calls to hang. Use a cancellation token with a 500 ms timeout. |
| Empty/null values | Common | App doesn't implement the requested pattern. Check `TryGetCurrentPattern` before accessing values. |

### 5.4 COM UIA3 vs. Managed `System.Windows.Automation`

| Aspect | `System.Windows.Automation` (UIA2) | COM `IUIAutomation` (UIA3) |
|---|---|---|
| API surface | Subset of UIA patterns | Full UIA3 specification |
| Performance | Slightly slower (extra marshalling layer) | Faster, direct COM calls |
| Threading | STA required | STA required |
| .NET 9 support | Available (Windows-only) | Via FlaUI or raw COM interop |
| Maintenance | No updates from Microsoft | Active (part of Windows SDK) |

**Recommendation:** Start with `System.Windows.Automation` for Phase 2 (URL extraction). If we need deeper UIA in Phase 3, migrate to FlaUI (UIA3 wrapper).

---

## 6. OCR Assessment

### 6.1 Windows.Media.Ocr (Built-in WinRT)

**How to call from .NET 9 WinForms:**

The project already targets `net9.0-windows10.0.22000.0`, which means WinRT APIs are directly accessible via the CsWinRT projection — no additional NuGet packages needed.

```csharp
using Windows.Media.Ocr;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

async Task<string?> ExtractTextFromScreenshot(string imagePath)
{
    var engine = OcrEngine.TryCreateFromUserProfileLanguages();
    if (engine == null) return null;

    using var stream = File.OpenRead(imagePath);
    var rasStream = stream.AsRandomAccessStream();
    var decoder = await BitmapDecoder.CreateAsync(rasStream);
    var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

    var result = await engine.RecognizeAsync(softwareBitmap);
    return result.Text;
}
```

| Aspect | Detail |
|---|---|
| **Accuracy** | Good for standard UI text (Latin scripts). Handles multiple fonts and sizes well. Weaker on very small text (<10px) and stylised/anti-aliased text. |
| **Performance** | 1920×1080: ~200-600 ms. 3840×2160 (4K): ~500-1500 ms. Uses GPU acceleration when available (via DirectX). |
| **Languages** | Supports all Windows language packs installed on the system. English is always available. |
| **Max image size** | 4096×4096 pixels (images larger than this are rejected). |
| **Threading** | Async API, safe to call from any thread. Internally uses the WinRT thread pool. |
| **Dependencies** | None — built into Windows 10+. |

**Verdict:** Best option for OCR. Zero dependencies, good accuracy, reasonable performance.

### 6.2 Tesseract (Open Source)

| Aspect | Detail |
|---|---|
| **NuGet** | `Tesseract` (v5.2+). Wrapper around native `leptonica` + `tesseract` C libraries. |
| **Accuracy** | Excellent with the `tessdata_best` model. Good with `tessdata_fast` (smaller, faster, slightly less accurate). |
| **Performance** | 1920×1080 (LSTM model): ~800-2000 ms. Faster with `tessdata_fast`: ~400-1000 ms. CPU-only, no GPU acceleration in the NuGet package. |
| **Model size** | `tessdata_fast/eng.traineddata`: ~4 MB. `tessdata_best/eng.traineddata`: ~15 MB. Must be distributed alongside the app. |
| **Threading** | Thread-safe — each `TesseractEngine` instance can be used on any thread. But creating an engine is expensive (~100 ms); reuse a singleton. |
| **Dependencies** | Native DLLs (`leptonica-1.82.0.dll`, `tesseract50.dll`) must be present. Complicates single-file deployment. |

**Verdict:** More accurate than WinRT OCR in edge cases, but the native dependency complicates deployment and the performance is worse. Not recommended unless WinRT OCR proves insufficient.

### 6.3 SkiaSharp Preprocessing

The project already uses SkiaSharp 3.x. Potential preprocessing steps before OCR:

1. **Downscale** to 1920×1080 if the source is 4K — reduces OCR time by 3-4×.
2. **Crop** to regions of interest: title bar area (top 60px), active window bounds, taskbar.
3. **Greyscale + threshold** — converts to high-contrast black/white, improving OCR accuracy on coloured backgrounds.
4. **Sharpen** — slight unsharp mask can improve small text recognition.

```csharp
using SkiaSharp;

SKBitmap PrepareForOcr(SKBitmap source, SKRectI cropRegion)
{
    // Crop to region of interest
    var cropped = new SKBitmap(cropRegion.Width, cropRegion.Height);
    using var canvas = new SKCanvas(cropped);
    canvas.DrawBitmap(source, cropRegion, new SKRect(0, 0, cropRegion.Width, cropRegion.Height));

    // Convert to greyscale
    using var paint = new SKPaint();
    paint.ColorFilter = SKColorFilter.CreateColorMatrix(new float[]
    {
        0.299f, 0.587f, 0.114f, 0, 0,
        0.299f, 0.587f, 0.114f, 0, 0,
        0.299f, 0.587f, 0.114f, 0, 0,
        0,      0,      0,      1, 0
    });

    var greyscale = new SKBitmap(cropped.Width, cropped.Height);
    using var gsCanvas = new SKCanvas(greyscale);
    gsCanvas.DrawBitmap(cropped, 0, 0, paint);

    return greyscale;
}
```

### 6.4 Recommended OCR Strategy

**Don't OCR every frame.** Instead:

1. **Phase 3 only** — OCR is a Phase 3 feature. Phase 1+2 signals (window title, process, URL) provide 80% of the context.
2. **Trigger-based OCR**: Run OCR only when the active window changes (suggesting new context), not on every tick.
3. **Crop, don't full-screen**: OCR only the top 100px of the active window (title/tab bar) and/or the focused text control area. This cuts OCR time by 10-20×.
4. **Use WinRT OCR**: Zero dependencies, good accuracy, GPU acceleration.
5. **Rate limit**: Maximum one OCR operation per 30 seconds, on a background thread.
6. **Store selectively**: Don't dump all OCR text into the activity record. Extract only "interesting" tokens: URLs, ticket numbers (regex: `[A-Z]+-\d+`), email addresses, file paths.

**Estimated latency with cropped region (800×100px): 20-50 ms** — negligible.

---

## 7. Privacy & Data Governance

### 7.1 Data That Must NEVER Be Logged

| Category | Detection Method | Action |
|---|---|---|
| **Passwords** | Window title contains "password", "sign in", "log in"; process is a known password manager (`1Password`, `KeePass`, `LastPass`, `Bitwarden`) | Skip entire activity record OR redact window title |
| **Credit card / financial data** | Process is a banking app or browser title contains bank names; clipboard contains 16-digit patterns | Exclude clipboard content; redact window title |
| **Private messaging content** | While chat apps (Teams, Slack) can be logged at the process/title level, actual message content (via UIA or OCR) must not be stored | Never use UIA/OCR to read message body text |
| **Medical / health data** | Browser titles containing health portal names | Configurable exclusion list |
| **Authentication tokens / secrets** | Clipboard content matching JWT, API key, or bearer token patterns | Filter clipboard content through regex exclusion |

### 7.2 Recommended Redaction Rules

```csharp
static class PrivacyFilter
{
    private static readonly string[] SensitiveProcesses =
    {
        "1password", "keepass", "lastpass", "bitwarden",
        "keychain", "credential"
    };

    private static readonly Regex[] SensitiveTitlePatterns =
    {
        new(@"\b(password|sign.?in|log.?in|2fa|mfa|otp)\b", RegexOptions.IgnoreCase),
        new(@"\b(credit.?card|payment|banking|checkout)\b", RegexOptions.IgnoreCase),
    };

    private static readonly Regex[] SensitiveClipboardPatterns =
    {
        new(@"\b\d{4}[\s-]?\d{4}[\s-]?\d{4}[\s-]?\d{4}\b"),  // Credit card numbers
        new(@"eyJ[A-Za-z0-9-_]+\.eyJ[A-Za-z0-9-_]+"),         // JWT tokens
        new(@"^[A-Za-z0-9+/]{40,}={0,2}$"),                    // Base64 secrets
        new(@"\b(sk-|pk-|api[_-]?key|bearer\s+)[A-Za-z0-9]{20,}", RegexOptions.IgnoreCase),
    };

    public static bool IsSensitiveProcess(string processName)
        => SensitiveProcesses.Any(s =>
            processName.Contains(s, StringComparison.OrdinalIgnoreCase));

    public static bool IsSensitiveTitle(string title)
        => SensitiveTitlePatterns.Any(r => r.IsMatch(title));

    public static string? SanitizeClipboard(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        if (SensitiveClipboardPatterns.Any(r => r.IsMatch(text))) return null;
        // Truncate long clipboard text
        return text.Length > 200 ? text[..200] : text;
    }
}
```

### 7.3 Sensitive Window Title Handling

**Recommended approach: configurable allowlist/blocklist.**

1. **Default blocklist** of known sensitive process names (password managers, banking apps).
2. When a blocklisted process is detected, log only: `{ "proc": "1password", "title": "[REDACTED]", "cat": "security" }`.
3. **User-configurable** exclusion list in `AppConfiguration`:
   ```json
   {
     "activityLogging": {
       "enabled": true,
       "excludedProcesses": ["1password", "keepass"],
       "excludeTitlePatterns": ["password", "banking"],
       "captureClipboard": false,
       "captureUrls": true
     }
   }
   ```

### 7.4 User Consent / Opt-In Design

| Principle | Implementation |
|---|---|
| **Opt-in by default** | `enableActivityLogging: false` in default config. User must explicitly enable. |
| **Clear disclosure** | First-time enable shows a dialog explaining what data is captured. |
| **Granular control** | Each signal type (clipboard, URLs, OCR) has its own enable/disable toggle. |
| **Data locality** | All data stays in local folders. No network transmission. |
| **Easy deletion** | "Clear activity data" button in settings. `CleanupService` already handles date-folder deletion. |
| **Visible indicator** | System tray icon changes when activity logging is active (e.g., small dot overlay). |
| **Session lock = pause** | Already implemented for screenshots. Activity logging must follow the same pattern. |

---

## 8. Integration Architecture

### 8.1 New Service: `ActivityLoggingService`

```
WindowsScreenLogger/
├── Services/
│   ├── ScreenshotService.cs      ← existing
│   ├── CleanupService.cs         ← existing
│   ├── ActivityLoggingService.cs  ← NEW: core activity capture
│   └── PrivacyFilter.cs          ← NEW: redaction/exclusion logic
├── Models/
│   └── ActivityRecord.cs         ← NEW: JSON-serialisable record
├── NativeMethods.cs              ← NEW: P/Invoke declarations
├── AppConfiguration.cs           ← MODIFIED: add activity logging settings
└── MainForm.cs                   ← MODIFIED: wire up service
```

### 8.2 Service Design

```csharp
public class ActivityLoggingService : IDisposable
{
    private readonly AppConfiguration config;
    private readonly ILogger logger;
    private readonly PrivacyFilter privacyFilter;
    private StreamWriter? currentWriter;
    private string? currentDateFolder;
    private string? lastProcessName;
    private string? lastWindowTitle;

    public ActivityLoggingService(AppConfiguration config, ILogger logger)
    {
        this.config = config;
        this.logger = logger;
        this.privacyFilter = new PrivacyFilter(config);
    }

    /// <summary>
    /// Captures current activity state and appends to the JSONL file.
    /// Called on each capture timer tick.
    /// </summary>
    public void CaptureActivity(string screenshotFilename, string dateFolderPath)
    {
        // 1. Get foreground window info (P/Invoke)
        // 2. Get idle time (P/Invoke)
        // 3. Apply privacy filter
        // 4. Classify process category
        // 5. Build ActivityRecord
        // 6. Serialize to JSON and append to activity.jsonl
    }

    public void Dispose() { currentWriter?.Dispose(); }
}
```

### 8.3 Timer Strategy: Same Tick vs. Independent

**Recommendation: Same timer tick as screenshot capture.**

Rationale:
- The activity record directly references the screenshot file (`"screen": "screenshot_142305.jpg"`).
- They share the same date folder.
- Activity capture (Phase 1) takes <1 ms — negligible overhead on the existing tick.
- Having two independent timers creates synchronisation complexity and potential race conditions on folder creation.

**Implementation in `MainForm.CaptureTimer_Tick`:**

```csharp
private async void CaptureTimer_Tick(object sender, EventArgs e)
{
    if (!isSessionLocked)
    {
        // Capture activity BEFORE screenshot (captures the "current" state)
        if (config.EnableActivityLogging)
        {
            var datePath = screenshotService.GetSavePath();
            var screenshotName = $"screenshot_{DateTime.Now:HHmmss}.jpg";
            activityLoggingService.CaptureActivity(screenshotName, datePath);
        }

        await CaptureAllScreensAsync();
    }
}
```

**Phase 2+ signals (clipboard, OCR) operate differently:**
- **Clipboard**: Event-driven (`WM_CLIPBOARDUPDATE`), not timer-based. The last clipboard text is cached and included in the next activity record.
- **OCR**: Rate-limited to once per 30-60 s, on a dedicated background thread. Results are cached and included in subsequent records until the next OCR run.

### 8.4 File I/O Strategy

- Use a `StreamWriter` with `AutoFlush = true` for the `.jsonl` file.
- Keep the writer open for the current date folder. When the date changes (midnight), close and open a new writer.
- Use `FileShare.Read` so the file can be read by other tools while the app is writing.
- On app shutdown, flush and close the writer.

```csharp
private void EnsureWriter(string dateFolderPath)
{
    if (currentDateFolder != dateFolderPath)
    {
        currentWriter?.Dispose();
        var filePath = Path.Combine(dateFolderPath, "activity.jsonl");
        var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
        currentWriter = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
        currentDateFolder = dateFolderPath;
    }
}
```

### 8.5 Configuration Extension

Add to `AppConfiguration`:

```csharp
[JsonPropertyName("activityLogging")]
public ActivityLoggingConfig ActivityLogging { get; set; } = new();

public class ActivityLoggingConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = false;

    [JsonPropertyName("captureClipboard")]
    public bool CaptureClipboard { get; set; } = false;

    [JsonPropertyName("captureUrls")]
    public bool CaptureUrls { get; set; } = true;

    [JsonPropertyName("ocrEnabled")]
    public bool OcrEnabled { get; set; } = false;

    [JsonPropertyName("ocrIntervalSeconds")]
    public int OcrIntervalSeconds { get; set; } = 60;

    [JsonPropertyName("excludedProcesses")]
    public List<string> ExcludedProcesses { get; set; } = new()
    {
        "1password", "keepass", "lastpass", "bitwarden"
    };

    [JsonPropertyName("excludeTitlePatterns")]
    public List<string> ExcludeTitlePatterns { get; set; } = new()
    {
        "password", "sign in", "banking"
    };
}
```

### 8.6 Dependency Injection

Follow the existing pattern in `MainForm`:

```csharp
public MainForm(
    AppConfiguration? configuration = null,
    ScreenshotService? screenshot = null,
    CleanupService? cleanup = null,
    ActivityLoggingService? activityLogger = null,  // NEW
    ILogger? logger = null)
```

This maintains testability — tests can inject a mock `ActivityLoggingService`.

---

## 9. Open Questions

| # | Question | Options | Recommendation |
|---|----------|---------|----------------|
| 1 | **Activity logging interval** — same as screenshot interval (default 5 s) or independent? | (a) Same tick (b) Independent timer (c) Configurable multiplier | **(a) Same tick** for Phase 1. Consider (c) for Phase 2+ when heavier signals are added. |
| 2 | **Deduplication** — skip record if nothing changed? | (a) Always write (b) Skip if process+title unchanged (c) Write but mark as duplicate | **(b) Skip if unchanged.** Reduces file size dramatically during focused work sessions. Still write periodic "heartbeat" records (e.g., every 60 s) even if unchanged. |
| 3 | **Privacy defaults** — opt-in or opt-out for each signal? | (a) All opt-in (b) Process+title opt-in, others opt-out (c) All opt-out | **(a) All opt-in.** Activity logging as a whole is opt-in; individual signals default to a safe subset (process, title, idle — no clipboard, no OCR). |
| 4 | **Data retention** — should activity data follow the same `ClearDays` as screenshots? | (a) Same policy (b) Separate retention (c) Configurable | **(a) Same policy.** Data lives in the same date folders and is cleaned up together. |
| 5 | **Maximum `.jsonl` file size** — cap it? | (a) No cap (b) Rotate at 10 MB (c) Rotate at 50 MB | **(a) No cap** for now. At ~1 MB/day the file is small. Revisit if OCR dumps large text blobs. |
| 6 | **Performance budget** — how many ms per tick is acceptable? | Hard limit? | **Recommend: <5 ms** for Phase 1, <50 ms for Phase 2 (including UIA URL query). Phase 3 OCR runs asynchronously, no tick budget. |
| 7 | **Multi-monitor** — log which monitor the active window is on? | (a) Yes (b) No | **(b) No** for now. The screenshot already captures all monitors. Not needed for AI summarisation. |
| 8 | **Incognito / private browsing** — should URLs be captured? | (a) Always (b) Never in incognito (c) Configurable | **(c) Configurable**, default to **(b)**. Detecting incognito: check window title for "InPrivate" / "Incognito" string. |
| 9 | **Elevated process fallback** — what to log when we can't read the exe path? | (a) Log process name only (b) Log "elevated" flag | **(a) + (b)**. Log `"proc": "taskmgr", "elevated": true, "exe": null`. |
| 10 | **Schema versioning** — how to handle future schema changes? | (a) Version field in each record (b) Version in filename (c) No versioning | **(a) Version field** — add `"v": 1` to each record. Costs 6 bytes. |

---

## 10. Recommended Approach (Executive Summary)

### What to Build

An **`ActivityLoggingService`** that captures structured metadata about user activity alongside existing screenshots. Data is stored as **JSON Lines** (`activity.jsonl`) in the same date-based folders, enabling the existing `CleanupService` to manage retention automatically.

### Implementation Order

**Phase 1 (1-2 days) — Ship this first:**
- P/Invoke `GetForegroundWindow`, `GetWindowText`, `GetWindowThreadProcessId`, `GetLastInputInfo`
- Capture: process name, PID, window title, executable path, idle seconds
- Dictionary-based category classification (browser/ide/office/comms/etc.)
- Privacy filter: blocklist of sensitive processes, pattern-based title redaction
- `ActivityRecord` model serialised with `System.Text.Json` (already in project)
- `activity.jsonl` file per date folder, appended on each screenshot timer tick
- Deduplication: skip write if process + title unchanged (heartbeat every 60 s)
- `enableActivityLogging` config flag (opt-in, default false)
- **Performance: <1 ms per tick**

**Phase 2 (2-3 days) — Rich context:**
- Browser URL extraction via `System.Windows.Automation` (Chrome + Edge)
- Clipboard monitoring via `WM_CLIPBOARDUPDATE` (text only, privacy-filtered)
- UWP process resolution (`ApplicationFrameHost` → real process)
- Configurable per-signal toggles in settings
- Incognito detection (skip URL capture when "InPrivate"/"Incognito" in title)
- **Performance: <15 ms per tick**

**Phase 3 (research-dependent) — Only if needed:**
- OCR via `Windows.Media.Ocr` on cropped screenshot regions (title bar, focused area)
- Run on window-change events, not every tick; rate-limited to 30 s minimum
- Extract structured tokens (ticket numbers, URLs, names) from OCR text via regex
- SkiaSharp preprocessing (greyscale + crop) before OCR
- **Performance: 20-50 ms per OCR run, async on background thread**

### APIs Used

| Signal | API | Dependency |
|--------|-----|------------|
| Foreground window | `user32.dll` P/Invoke | None (built-in) |
| Idle detection | `user32.dll` P/Invoke | None (built-in) |
| JSON serialisation | `System.Text.Json` | Already in project |
| Browser URL | `System.Windows.Automation` | Built-in (Windows) |
| Clipboard | `user32.dll` P/Invoke + WinForms `Clipboard` | None (built-in) |
| OCR | `Windows.Media.Ocr` (WinRT) | None (built into Windows, accessible via TFM) |
| Image preprocessing | SkiaSharp | Already in project |

**Zero new NuGet dependencies for Phases 1 and 2.** Phase 3 OCR also requires no new packages thanks to the WinRT target framework.

### Key Design Decisions

1. **Same timer tick** as screenshots — keeps the activity record tightly coupled to its screenshot.
2. **Opt-in** — all activity logging is off by default. User explicitly enables it.
3. **Privacy-first** — sensitive processes are blocklisted; clipboard content is filtered; no keystroke logging ever.
4. **Append-only JSONL** — simple, fast, corruption-resistant. One record per line, one file per day.
5. **Schema version field** (`"v": 1`) — future-proofs the format.
6. **No new threads in Phase 1** — everything runs on the existing `Task.Run` path. Phase 2 adds an STA thread for UIA. Phase 3 adds an async OCR task.

### Expected Output (Example Day)

```jsonl
{"v":1,"ts":"2026-03-19T09:00:05Z","proc":"outlook","title":"Inbox - user@company.com - Outlook","idle":1,"cat":"communication","screen":"screenshot_090005.jpg"}
{"v":1,"ts":"2026-03-19T09:02:35Z","proc":"chrome","title":"JIRA-4532 - Sprint Board - Google Chrome","idle":0,"cat":"browser","ctx":{"url":"https://jira.company.com/browse/JIRA-4532"},"screen":"screenshot_090235.jpg"}
{"v":1,"ts":"2026-03-19T09:15:10Z","proc":"code","title":"auth.service.ts - my-project - Visual Studio Code","idle":3,"cat":"ide","screen":"screenshot_091510.jpg"}
{"v":1,"ts":"2026-03-19T09:45:00Z","proc":"teams","title":"Meeting: Sprint Planning - Microsoft Teams","idle":0,"cat":"communication","screen":"screenshot_094500.jpg"}
```

From these records alone, an AI can produce:
> *"Morning: Checked email in Outlook, then worked on JIRA-4532 (sprint board in Chrome). Edited auth.service.ts in VS Code for ~30 minutes. Attended Sprint Planning meeting in Teams from 9:45."*

**This is the minimum viable activity capture. Phase 1 delivers it with zero new dependencies and <1 ms overhead.**

---

*End of research document. Ready for review and sign-off before implementation begins.*
