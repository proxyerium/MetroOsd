using Windows.Win32;

namespace MetroOsd;

/// <summary>Debug logging. Debug builds attach a console and print; release builds stay silent.</summary>
internal static class Log
{
    static Log()
    {
#if DEBUG
        // Attach a console so messages are visible; a no-op when one already exists.
        PInvoke.AllocConsole();
#endif
    }

    /// <summary>Routine status line, formatted exactly like the legacy output.</summary>
    public static void Info(string message) => Write(string.Empty, message);

    /// <summary>Failure line; gets an ERROR tag so it stands out from status lines.</summary>
    public static void Error(string message) => Write("ERROR ", message);

    private static void Write(string levelTag, string message)
    {
#if DEBUG
        Console.WriteLine($"[MetroOsd {DateTime.Now:HH:mm:ss.fff}] {levelTag}{message}");
#endif
    }
}
