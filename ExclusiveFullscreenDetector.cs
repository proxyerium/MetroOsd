using Windows.Win32;
using Windows.Win32.UI.Shell;

namespace MetroOsd;

/// <summary>
/// Reports whether a Direct3D exclusive-fullscreen application currently owns the screen,
/// using the same shell signal Windows uses to suppress its own notifications and toasts.
///
/// Showing a topmost overlay while such an app is focused can force it out of exclusive
/// fullscreen / minimize it, so the OSD is suppressed in that state.
/// </summary>
internal static class ExclusiveFullscreenDetector
{
    /// <summary>
    /// True when the shell reports a running D3D exclusive-fullscreen app. Any interop failure
    /// is treated as "not fullscreen" so the OSD keeps working exactly as before.
    /// </summary>
    public static bool IsActive()
    {
        var hr = PInvoke.SHQueryUserNotificationState(out QUERY_USER_NOTIFICATION_STATE state);
        if (hr.Failed)
        {
            Log.Error($"SHQueryUserNotificationState failed, hr=0x{hr.Value:X8}");
            return false;
        }

        return state == QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN;
    }
}
