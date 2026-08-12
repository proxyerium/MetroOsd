namespace MetroOsd;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, @"Local\MetroOsd.SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            return;
        }

        ApplicationConfiguration.Initialize();

        var controller = new OsdController();
        try
        {
            controller.Start();
            Application.ApplicationExit += (_, _) => controller.Dispose();
            Application.Run(new ApplicationContext());
        }
        finally
        {
            controller.Dispose();
        }
    }
}
