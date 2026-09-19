using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MetroOsd;

/// <summary>
/// Raises an event whenever the foreground window changes, so the overlay can react at the
/// moment the user switches to an exclusive-fullscreen app — before Windows weighs the
/// still-visible topmost overlay against that app's fullscreen mode.
/// </summary>
internal sealed class ForegroundWatcher : IDisposable
{
    // Win32 event constants (not exposed as CsWin32 generation targets).
    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

    private readonly WINEVENTPROC _proc;
    private HWINEVENTHOOK _hook;

    /// <summary>Raised when the foreground window changes to a window outside this process.</summary>
    public event Action? ForegroundChanged;

    public ForegroundWatcher()
    {
        _proc = OnWinEvent;
    }

    public void Start()
    {
        _hook = PInvoke.SetWinEventHook(
            EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
            default(HMODULE), _proc, 0, 0,
            WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);

        if (_hook.IsNull)
        {
            Log.Error($"SetWinEventHook(EVENT_SYSTEM_FOREGROUND) failed, error={Marshal.GetLastWin32Error()}");
        }
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

        ForegroundChanged?.Invoke();
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
