using System;
using System.IO;
using System.Text;

namespace DesktopSnow
{
    internal static class AppLogger
    {
        private static readonly object SyncRoot = new object();

        public static void Write(string message, Exception exception)
        {
            try
            {
                string directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DesktopSnow");
                Directory.CreateDirectory(directory);

                string path = Path.Combine(directory, "DesktopSnow.log");
                StringBuilder text = new StringBuilder();
                text.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message);
                if (exception != null)
                {
                    text.AppendLine(exception.ToString());
                }
                text.AppendLine();

                lock (SyncRoot)
                {
                    File.AppendAllText(path, text.ToString(), Encoding.UTF8);
                }
            }
            catch
            {
                // Logging must never terminate the visual effect.
            }
        }
    }
}
