using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MetroOsd;

/// <summary>
/// Tracks the explorer-owned native OSD window (class <c>NativeHWNDHost</c>, no caption,
/// usually with a <c>DirectUIHWND</c> child).
///
/// Discovery notes:
///  - The OSD is created lazily on the first key event and then kept alive (hidden).
///  - While hidden the <c>DirectUIHWND</c> child may not exist yet, so the child must NOT be a
///    hard requirement: a childless candidate is accepted at startup and re-validated when it
///    first shows (the child appears by then).
///  - The window may not be a top-level window, so event-based capture does not require
///    top-level; the startup EnumWindows scan naturally sees only top-levels.
///  - One always-on WinEvent hook (CREATE..HIDE) plus an EnumWindows re-scan fallback on every
///    key event keeps the handle fresh across explorer restarts.
/// </summary>
internal sealed class NativeOsdWatcher : IDisposable
{
    private const string OsdClassName = "NativeHWNDHost";
    private const string ChildClassName = "DirectUIHWND";

    // Win32 event constants (not exposed as CsWin32 generation targets).
    private const uint EVENT_OBJECT_CREATE = 0x8000;
    private const uint EVENT_OBJECT_DESTROY = 0x8001;
    private const uint EVENT_OBJECT_SHOW = 0x8002;
    private const uint EVENT_OBJECT_HIDE = 0x8003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    private readonly WINEVENTPROC _proc;

    private HWND _hwnd;
    private RECT _rect;
    private HWINEVENTHOOK _hook;

    /// <summary>Raised with the fresh rect when the captured native OSD becomes visible.</summary>
    public event Action<RECT>? NativeOsdVisible;

    /// <summary>Raised with the last known rect when the captured native OSD hides.</summary>
    public event Action<RECT>? NativeOsdHidden;

    public NativeOsdWatcher()
    {
        _proc = OnWinEvent;
    }

    public void Start()
    {
        _hook = PInvoke.SetWinEventHook(
            EVENT_OBJECT_CREATE, EVENT_OBJECT_HIDE,
            default(HMODULE), _proc, 0, 0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);

        if (_hook.IsNull)
        {
            Log.Error($"SetWinEventHook failed, error={Marshal.GetLastWin32Error()}");
        }
        else
        {
            Log.Info("WinEvent hook armed (CREATE..HIDE)");
        }

        if (FindExisting())
        {
            Log.Info($"native OSD captured on startup: hwnd={HwndHex(_hwnd)}, owner={OwnerName(_hwnd)}, hasChild={HasDirectUIHWNDChild(_hwnd)}, rect={_rect}");
        }
        else
        {
            Log.Info("native OSD not found yet; will capture on CREATE/SHOW event or next key event");
        }
    }

    /// <summary>
    /// Latest native OSD placement plus live visibility. Re-scans once when the captured handle
    /// is gone. Returns false only when no OSD is known at all (caller then uses its own
    /// default position).
    /// </summary>
    public bool TryGetPlacement(out RECT rect, out bool visible)
    {
        visible = false;
        if (_hwnd.IsNull || !PInvoke.IsWindow(_hwnd))
        {
            _hwnd = default;
            if (!FindExisting())
            {
                rect = default;
                return false;
            }
        }

        PInvoke.GetWindowRect(_hwnd, out rect);
        visible = PInvoke.IsWindowVisible(_hwnd);
        _rect = rect;
        return true;
    }

    /// <summary>
    /// Scans top-level windows for the OSD. Prefers a candidate that already has a
    /// DirectUIHWND child; accepts a childless candidate only when no better one exists.
    /// </summary>
    private bool FindExisting()
    {
        HWND withChild = default;
        HWND childlessExplorer = default;
        HWND childlessAny = default;

        PInvoke.EnumWindows((hwnd, _) =>
        {
            if (!IsOsdWindowCore(hwnd))
            {
                return true;
            }

            if (HasDirectUIHWNDChild(hwnd))
            {
                withChild = hwnd;
                return false; // best match, stop
            }

            string? owner = GetOwnerProcessName(hwnd);
            if (string.Equals(owner, "explorer", StringComparison.OrdinalIgnoreCase) && childlessExplorer.IsNull)
            {
                childlessExplorer = hwnd;
            }
            if (childlessAny.IsNull)
            {
                childlessAny = hwnd;
            }
            return true;
        }, default);

        HWND found = !withChild.IsNull ? withChild : !childlessExplorer.IsNull ? childlessExplorer : childlessAny;
        if (found.IsNull)
        {
            return false;
        }

        _hwnd = found;
        PInvoke.GetWindowRect(found, out _rect);
        return true;
    }

    /// <summary>
    /// Relaxed identification: class NativeHWNDHost + no caption + DirectUIHWND child.
    /// No top-level requirement (event capture must also accept non-top-level OSD hosts).
    /// </summary>
    private bool IsOsdWindowCore(HWND hwnd)
    {
        Span<char> buf = stackalloc char[256];
        int len = PInvoke.GetClassName(hwnd, buf);
        if (len <= 0 || new string(buf[..len]) != OsdClassName)
        {
            return false;
        }

        // No caption.
        int style = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        if ((style & (int)WINDOW_STYLE.WS_CAPTION) != 0)
        {
            return false;
        }

        // Must host a DirectUIHWND child.
        if (!HasDirectUIHWNDChild(hwnd))
        {
            return false;
        }

        return true;
    }

    private static bool HasDirectUIHWNDChild(HWND hwnd)
        => !PInvoke.FindWindowEx(hwnd, HWND.Null, ChildClassName, null).IsNull;

    private void OnWinEvent(
        HWINEVENTHOOK hWinEventHook,
        uint @event,
        HWND hwnd,
        int idObject,
        int idChild,
        uint idEventThread,
        uint dwmsEventTime)
    {
        if (idObject != (int)OBJECT_IDENTIFIER.OBJID_WINDOW || idChild != 0)
        {
            return;
        }

        // Cheap pre-filter by class before doing anything else.
        Span<char> buf = stackalloc char[64];
        int len = PInvoke.GetClassName(hwnd, buf);
        if (len <= 0 || new string(buf[..len]) != OsdClassName)
        {
            return;
        }

        if (@event == EVENT_OBJECT_CREATE)
        {
            if (!IsOsdWindowCore(hwnd))
            {
                return;
            }

            Capture(hwnd, "CREATE");
        }
        else if (@event == EVENT_OBJECT_SHOW)
        {
            if (_hwnd.IsNull || hwnd != _hwnd)
            {
                // Pre-existing OSD we have not captured yet -> capture on first show.
                if (!IsOsdWindowCore(hwnd))
                {
                    return;
                }
                Capture(hwnd, "SHOW");
            }

            PInvoke.GetWindowRect(hwnd, out _rect);
            Log.Info("native OSD visible");
            NativeOsdVisible?.Invoke(_rect);
        }
        else if (@event == EVENT_OBJECT_HIDE)
        {
            if (hwnd == _hwnd)
            {
                Log.Info("native OSD hidden");
                NativeOsdHidden?.Invoke(_rect);
            }
        }
        else if (@event == EVENT_OBJECT_DESTROY)
        {
            if (hwnd == _hwnd)
            {
                Log.Info("native OSD destroyed; will re-capture on next CREATE/SHOW");
                _hwnd = default;
            }
        }
    }

    private void Capture(HWND hwnd, string via)
    {
        _hwnd = hwnd;
        PInvoke.GetWindowRect(hwnd, out _rect);
        Log.Info($"native OSD captured ({via}): hwnd={HwndHex(hwnd)}, owner={OwnerName(hwnd)}, hasChild={HasDirectUIHWNDChild(hwnd)}, rect={_rect}");
    }

    private static unsafe string HwndHex(HWND hwnd) => $"0x{(nint)hwnd.Value:X}";

    private static string OwnerName(HWND hwnd)
    {
        PInvoke.GetWindowThreadProcessId(hwnd, out uint pid);
        try
        {
            return $"{Process.GetProcessById((int)pid).ProcessName} ({pid})";
        }
        catch (ArgumentException)
        {
            return $"pid {pid}";
        }
    }

    private static string? GetOwnerProcessName(HWND hwnd)
    {
        PInvoke.GetWindowThreadProcessId(hwnd, out uint pid);
        try
        {
            return Process.GetProcessById((int)pid).ProcessName;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (!_hook.IsNull)
        {
            PInvoke.UnhookWinEvent(_hook);
            _hook = default;
        }
    }
}
