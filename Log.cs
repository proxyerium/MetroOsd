namespace MetroOsd;

/// <summary>Debug logging. Debug builds attach a console and print; release builds stay silent.</summary>
internal static class Log
{
    public static void Info(string message)
    {
#if DEBUG
        Console.WriteLine($"[MetroOsd {DateTime.Now:HH:mm:ss.fff}] {message}");
#endif
    }
}
