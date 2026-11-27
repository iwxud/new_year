using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace DesktopSnow
{
    internal static class Program
    {
        private const string MutexName = @"Local\DesktopSnow.F4D8C310-8EE5-42E8-A856-74EEB43A8E9B";

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll", EntryPoint = "SetProcessDpiAwarenessContext")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

        [STAThread]
        public static void Main()
        {
            EnableDpiAwareness();

            bool createdNew;
            using (Mutex mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show(
                        "Desktop Snow уже запущен.\n\nНастройки находятся в значке снежинки рядом с часами.",
                        "Desktop Snow",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                SnowApplication application = new SnowApplication();
                application.Run();

                GC.KeepAlive(mutex);
            }
        }

        private static void EnableDpiAwareness()
        {
            try
            {
                // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
                if (SetProcessDpiAwarenessContext(new IntPtr(-4)))
                {
                    return;
                }
            }
            catch (EntryPointNotFoundException)
            {
            }
            catch (DllNotFoundException)
            {
            }

            try
            {
                SetProcessDPIAware();
            }
            catch
            {
                // Windows versions supported by the app normally expose one of
                // the two APIs. The manifest remains as an additional fallback.
            }
        }
    }
}
