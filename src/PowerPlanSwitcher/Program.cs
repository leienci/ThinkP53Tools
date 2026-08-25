using System.Threading;

namespace PowerPlanSwitcher;

internal static class Program
{
    private const string MutexName = @"Global\PowerPlanSwitcher_Mutex_A1B2C3D4";

    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.SystemAware);

        using var mutex = new Mutex(false, MutexName);
        if (!mutex.WaitOne(0, false))
            return;

        try
        {
            var settings = IniSettings.Load();
            var host = new ApplicationHost(settings);
            host.Run();
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }
}
