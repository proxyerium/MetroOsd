using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MetroOsd;

/// <summary>
/// Tracks the explorer-owned native OSD window (class <c>NativeHWNDHost</c>, parent = Desktop,
/// no caption, child <c>DirectUIHWND</c>).
///
/// Robust discovery strategy (the OSD is created lazily on the first key event and then kept
/// alive, so a pure EVENT_OBJECT_CREATE wait deadlocks once the OSD already exists):
///  - ONE always-on WinEvent hook over CREATE..HIDE (0x8000..0x8003);
///  - every event for a <c>NativeHWNDHost</c> window is validated and may capture it
///    (CREATE captures a fresh OSD; SHOW captures a pre-existing OSD the moment it appears);
///  - DESTROY invalidates and the next key event re-scans (EnumWindows) as a fallback.
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
            Log.Info($"SetWinEventHook failed, error={Marshal.GetLastWin32Error()}");
        }
        else
        {
            Log.Info("WinEvent hook armed (CREATE..HIDE)");
        }

        if (FindExisting())
        {
            Log.Info($"native OSD found on startup: hwnd={HwndHex(_hwnd)}, rect={_rect}");
        }
        else
        {
            Log.Info("native OSD not found yet; will capture on CREATE/SHOW event or next key event");
        }
    }

    /// <summary>
    /// Latest native OSD placement plus live visibility. Re-scans once when the captured handle
    /// is gone. Returns false only when no OSD is known at all (caller must then not show).
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

    private bool FindExisting()
    {
        HWND found = default;
        PInvoke.EnumWindows((hwnd, _) =>
        {
            if (IsOsdWindowCore(hwnd))
            {
                found = hwnd;
                return false; // stop enumerating
            }
            return true;
        }, default);

        if (!found.IsNull)
        {
            _hwnd = found;
            PInvoke.GetWindowRect(found, out _rect);
            return true;
        }
        return false;
    }

    private bool IsOsdWindowCore(HWND hwnd)
    {
        // Must be a top-level window (parent = Desktop). EnumWindows only yields top-level
        // windows, but WinEvent callbacks also see children, so verify explicitly.
        if (!PInvoke.GetParent(hwnd).IsNull)
        {
            return false;
        }

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
        if (PInvoke.FindWindowEx(hwnd, HWND.Null, ChildClassName, null).IsNull)
        {
            return false;
        }

        return true;
    }

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

            _hwnd = hwnd;
            PInvoke.GetWindowRect(hwnd, out _rect);
            Log.Info($"native OSD captured (CREATE): hwnd={HwndHex(hwnd)}, owner={OwnerName(hwnd)}, rect={_rect}");
        }
        else if (@event == EVENT_OBJECT_SHOW)
        {
            if (_hwnd.IsNull || hwnd != _hwnd)
            {
                // Pre-existing OSD that we have not captured yet -> capture on first show.
                if (!IsOsdWindowCore(hwnd))
                {
                    return;
                }
                _hwnd = hwnd;
                PInvoke.GetWindowRect(hwnd, out _rect);
                Log.Info($"native OSD captured (SHOW): hwnd={HwndHex(hwnd)}, owner={OwnerName(hwnd)}, rect={_rect}");
                return;
            }

            PInvoke.GetWindowRect(hwnd, out _rect);
            Log.Info("native OSD visible");
        }
        else if (@event == EVENT_OBJECT_HIDE)
        {
            if (hwnd == _hwnd)
            {
                Log.Info("native OSD hidden");
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

    public void Dispose()
    {
        if (!_hook.IsNull)
        {
            PInvoke.UnhookWinEvent(_hook);
            _hook = default;
        }
    }
}

