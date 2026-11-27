using System;
using System.Reflection;
using Microsoft.Win32;

namespace DesktopSnow
{
    internal enum SnowIntensity
    {
        Light = 0,
        Normal = 1,
        Heavy = 2
    }

    internal sealed class AppSettings
    {
        private const string SettingsPath = @"Software\DesktopSnow";
        private const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "DesktopSnow";

        public SnowIntensity Intensity { get; set; }
        public bool AlwaysOnTop { get; set; }
        public bool WelcomeWasShown { get; set; }

        public AppSettings()
        {
            Intensity = SnowIntensity.Normal;
            AlwaysOnTop = false;
        }

        public static AppSettings Load()
        {
            AppSettings settings = new AppSettings();

            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(SettingsPath))
                {
                    int intensity = ReadInt(key, "Intensity", (int)SnowIntensity.Normal);
                    if (intensity >= (int)SnowIntensity.Light && intensity <= (int)SnowIntensity.Heavy)
                    {
                        settings.Intensity = (SnowIntensity)intensity;
                    }

                    settings.AlwaysOnTop = ReadInt(key, "AlwaysOnTop", 0) != 0;
                    settings.WelcomeWasShown = ReadInt(key, "WelcomeWasShown", 0) != 0;
                }
            }
            catch (Exception exception)
            {
                AppLogger.Write("Не удалось прочитать настройки.", exception);
            }

            return settings;
        }

        public void Save()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(SettingsPath))
                {
                    key.SetValue("Intensity", (int)Intensity, RegistryValueKind.DWord);
                    key.SetValue("AlwaysOnTop", AlwaysOnTop ? 1 : 0, RegistryValueKind.DWord);
                    key.SetValue("WelcomeWasShown", WelcomeWasShown ? 1 : 0, RegistryValueKind.DWord);
                }
            }
            catch (Exception exception)
            {
                AppLogger.Write("Не удалось сохранить настройки.", exception);
            }
        }

        public static bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunPath, false))
                {
                    return key != null && key.GetValue(RunValueName) != null;
                }
            }
            catch (Exception exception)
            {
                AppLogger.Write("Не удалось проверить автозапуск.", exception);
                return false;
            }
        }

        public static void SetStartupEnabled(bool enabled)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunPath))
                {
                    if (enabled)
                    {
                        key.SetValue(RunValueName, BuildStartupCommand(), RegistryValueKind.String);
                    }
                    else
                    {
                        key.DeleteValue(RunValueName, false);
                    }
                }
            }
            catch (Exception exception)
            {
                AppLogger.Write("Не удалось изменить автозапуск.", exception);
                throw;
            }
        }

        public static void RefreshStartupPathIfEnabled()
        {
            if (!IsStartupEnabled())
            {
                return;
            }

            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunPath))
                {
                    key.SetValue(RunValueName, BuildStartupCommand(), RegistryValueKind.String);
                }
            }
            catch (Exception exception)
            {
                AppLogger.Write("Не удалось обновить путь автозапуска.", exception);
            }
        }

        private static string BuildStartupCommand()
        {
            string executable = Assembly.GetExecutingAssembly().Location;
            return "\"" + executable + "\" --startup";
        }

        private static int ReadInt(RegistryKey key, string name, int defaultValue)
        {
            object value = key.GetValue(name, defaultValue);
            if (value is int)
            {
                return (int)value;
            }

            int parsed;
            return int.TryParse(Convert.ToString(value), out parsed) ? parsed : defaultValue;
        }
    }
}
