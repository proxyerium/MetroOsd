using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace MetroOsd;

/// <summary>
/// Wires the keyboard hook to the native OSD watcher and the overlay form.
/// Positioning: native OSD visible → overlay directly below it; hidden → overlay at the native
/// OSD's own position. No native OSD captured yet → nothing is shown.
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
        _watcher.Start();
        _keyboardHook.KeyPressed += OnKeyPressed;
        Log.Info("started");
    }

    private void OnKeyPressed()
    {
        bool capsOn = (PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_CAPITAL) & 1) != 0;
        string text = capsOn ? "Caps Lock ON" : "Caps Lock OFF";

        if (!_watcher.TryGetPlacement(out RECT rect, out bool nativeVisible))
        {
            Log.Info($"caps={capsOn}: skipped (native OSD not captured yet)");
            return;
        }

        int x = rect.left;
        int y = nativeVisible ? rect.bottom + Gap : rect.top;
        Log.Info($"caps={capsOn}: nativeVisible={nativeVisible}, pos=({x},{y}), nativeRect=({rect.left},{rect.top},{rect.right},{rect.bottom})");
        _form.ShowOsd(text, new Point(x, y));
    }

    public void Dispose()
    {
        _keyboardHook.Dispose();
        _watcher.Dispose();
        _form.Dispose();
    }
}
