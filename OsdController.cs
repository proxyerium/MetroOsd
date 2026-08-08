using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace MetroOsd;

/// <summary>
/// Wires the keyboard hook to the native OSD watcher and the overlay form.
///
/// The new OSD is independent: it always answers CapsLock. When the native OSD has been
/// captured it is positioned relative to it (below when visible, at its spot when hidden);
/// otherwise it uses the native OSD's usual spot (top-left). When the native OSD becomes
/// visible while our overlay is up, the overlay is moved directly below it.
/// </summary>
internal sealed class OsdController : IDisposable
{
    private const int Gap = 6;

    private readonly KeyboardHook _keyboardHook;
    private readonly NativeOsdWatcher _watcher;
    private readonly OsdForm _form;

    public OsdController()
    {
        _keyboardHook = new KeyboardHook();
        _watcher = new NativeOsdWatcher();
        _form = new OsdForm();
    }

    public void Start()
    {
        _watcher.NativeOsdVisible += OnNativeOsdVisible;
        _watcher.NativeOsdHidden += OnNativeOsdHidden;
        _watcher.Start();
        _keyboardHook.KeyPressed += OnKeyPressed;
        Log.Info("started");
    }

    private void OnKeyPressed()
    {
        bool capsOn = (PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_CAPITAL) & 1) != 0;
        string text = capsOn ? Resources.CapsLockOn : Resources.CapsLockOff;

        Point pos;
        if (_watcher.TryGetPlacement(out RECT rect, out bool nativeVisible))
        {
            pos = nativeVisible ? Below(rect) : At(rect);
            Log.Info($"caps={capsOn}: nativeVisible={nativeVisible}, pos=({pos.X},{pos.Y}), nativeRect=({rect.left},{rect.top},{rect.right},{rect.bottom})");
        }
        else
        {
            // Native OSD not captured: the new OSD stands alone at the native OSD's usual spot.
            pos = DefaultPosition();
            Log.Info($"caps={capsOn}: native OSD not captured, using default pos=({pos.X},{pos.Y})");
        }

        _form.ShowOsd(text, pos);
    }

    private void OnNativeOsdVisible(RECT rect)
    {
        // Native OSD just showed while our overlay is up: move it directly below.
        if (!_form.Visible)
        {
            return;
        }

        Point pos = Below(rect);
        Log.Info($"native OSD visible -> reposition overlay to ({pos.X},{pos.Y})");
        _form.MoveTo(pos);
    }

    private void OnNativeOsdHidden(RECT rect)
    {
        // Native OSD just hid while our overlay is up: move it up to the native spot.
        if (!_form.Visible)
        {
            return;
        }

        Point pos = At(rect);
        Log.Info($"native OSD hidden -> reposition overlay to ({pos.X},{pos.Y})");
        _form.MoveTo(pos);
    }

    private Point Below(RECT rect) => new(rect.left, rect.bottom + Gap);

    private Point At(RECT rect) => new(rect.left, rect.top);

    private static Point DefaultPosition() => new(62, 75);

    public void Dispose()
    {
        _watcher.NativeOsdVisible -= OnNativeOsdVisible;
        _watcher.NativeOsdHidden -= OnNativeOsdHidden;
        _keyboardHook.Dispose();
        _watcher.Dispose();
        _form.Dispose();
    }
}
