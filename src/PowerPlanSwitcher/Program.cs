using System.Threading;
using Microsoft.Win32;

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

        ApplicationHost? host = null;
        // 兜底：即便窗口尚未接收到 WM_QUERYENDSESSION（例如窗口未创建即被强制关机），
        // SystemEvents.SessionEnding 也会在关机/注销时被触发，避免定时器继续调 powercfg.exe。
        SessionEndingEventHandler sessionEndingHandler = (_, e) =>
        {
            try { host?.Exit(); } catch { }
            e.Cancel = false;
        };
        SystemEvents.SessionEnding += sessionEndingHandler;

        try
        {
            var settings = IniSettings.Load();
            host = new ApplicationHost(settings);
            host.Run();
        }
        finally
        {
            SystemEvents.SessionEnding -= sessionEndingHandler;
            try { host?.Exit(); } catch { }
            mutex.ReleaseMutex();
        }
    }
}
