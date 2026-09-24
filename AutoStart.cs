using System.Reflection;
using Microsoft.Win32;

namespace MetroOsd;

/// <summary>
/// Registers MetroOsd to auto-start at logon.
///
/// Runs once, on first launch, and only for the installed build (the executable must
/// live under Program Files / Program Files (x86)). Debug and portable runs are left
/// alone so they never pollute the Run key.
///
/// A marker value (HKCU\Software\MetroOsd\AutoStartConfigured) records that the first
/// launch already happened, so subsequent launches leave the Run key untouched and the
/// user can still disable auto-start by deleting the Run value.
/// </summary>
internal static class AutoStart
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "MetroOsd";
    private const string MarkerKeyPath = @"Software\MetroOsd";
    private const string MarkerValueName = "AutoStartConfigured";

    /// <summary>Checks the Run key on first launch and registers the current executable.</summary>
    public static void EnsureRegistered()
    {
        try
        {
            string exePath = Assembly.GetEntryAssembly()?.Location ?? string.Empty;
            if (exePath.Length == 0 || !IsInstalled(exePath))
            {
                Log.Info("autostart skipped (not running from Program Files)");
                return;
            }

            // First launch already handled -> leave the Run key alone.
            using (RegistryKey? marker = Registry.CurrentUser.OpenSubKey(MarkerKeyPath, writable: true))
            {
                if (marker?.GetValue(MarkerValueName) is not null)
                {
                    Log.Info("autostart already configured on first launch; Run key left untouched");
                    return;
                }
            }

            using RegistryKey runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                                       ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (runKey is null)
            {
                Log.Error("autostart: cannot open/create the Run key");
                return;
            }

            string expected = "\"" + exePath + "\"";
            if (runKey.GetValue(ValueName) is string existing
                && string.Equals(existing, expected, StringComparison.OrdinalIgnoreCase))
            {
                Log.Info($"autostart already registered: {expected}");
            }
            else
            {
                runKey.SetValue(ValueName, expected);
                Log.Info($"autostart registered: {expected}");
            }

            using RegistryKey markerKey = Registry.CurrentUser.CreateSubKey(MarkerKeyPath);
            markerKey.SetValue(MarkerValueName, 1, RegistryValueKind.DWord);
        }
        catch (Exception ex)
        {
            Log.Error($"autostart failed: {ex.Message}");
        }
    }

    private static bool IsInstalled(string exePath)
    {
        string dir = Path.GetDirectoryName(exePath) ?? string.Empty;
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        return dir.StartsWith(programFiles + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || dir.StartsWith(programFilesX86 + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }
}
