using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace DesktopSnow
{
    internal sealed class SnowApplication : Application
    {
        private readonly List<SnowWindow> _windows;
        private readonly Stopwatch _frameClock;

        private AppSettings _settings;
        private DispatcherTimer _animationTimer;
        private DispatcherTimer _displayTimer;
        private DispatcherTimer _zOrderTimer;
        private Forms.NotifyIcon _notifyIcon;
        private Forms.ToolStripMenuItem _pauseItem;
        private Forms.ToolStripMenuItem _lightItem;
        private Forms.ToolStripMenuItem _normalItem;
        private Forms.ToolStripMenuItem _heavyItem;
        private Forms.ToolStripMenuItem _topmostItem;
        private Forms.ToolStripMenuItem _startupItem;
        private Icon _applicationIcon;
        private bool _paused;
        private bool _exiting;
        private string _displaySignature;

        private static readonly IntPtr HwndTopmost = new IntPtr(-1);
        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoMove = 0x0002;
        private const uint SwpNoActivate = 0x0010;

        public SnowApplication()
        {
            _windows = new List<SnowWindow>();
            _frameClock = new Stopwatch();
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            DispatcherUnhandledException += HandleDispatcherException;
            AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;
        }

        protected override void OnStartup(StartupEventArgs eventArgs)
        {
            base.OnStartup(eventArgs);

            _settings = AppSettings.Load();
            AppSettings.RefreshStartupPathIfEnabled();

            CreateTrayIcon();
            RebuildWindows();
            StartTimers();

            if (!_settings.WelcomeWasShown)
            {
                _notifyIcon.ShowBalloonTip(
                    4500,
                    "Desktop Snow запущен",
                    "Настройки, пауза и выход — правой кнопкой по снежинке рядом с часами.",
                    Forms.ToolTipIcon.Info);
                _settings.WelcomeWasShown = true;
                _settings.Save();
            }
        }

        protected override void OnExit(ExitEventArgs eventArgs)
        {
            DisposeResources();
            base.OnExit(eventArgs);
        }

        private void StartTimers()
        {
            _frameClock.Restart();

            _animationTimer = new DispatcherTimer(DispatcherPriority.Render);
            _animationTimer.Interval = TimeSpan.FromMilliseconds(33.0);
            _animationTimer.Tick += HandleAnimationTick;
            _animationTimer.Start();

            _displayTimer = new DispatcherTimer(DispatcherPriority.Background);
            _displayTimer.Interval = TimeSpan.FromSeconds(2.0);
            _displayTimer.Tick += HandleDisplayTick;
            _displayTimer.Start();

            // Periodically push the snow windows back to the bottom of the
            // z-order so no application ever ends up covered by the snow.
            _zOrderTimer = new DispatcherTimer(DispatcherPriority.Background);
            _zOrderTimer.Interval = TimeSpan.FromSeconds(1.0);
            _zOrderTimer.Tick += HandleZOrderTick;
            _zOrderTimer.Start();
        }

        private void HandleZOrderTick(object sender, EventArgs eventArgs)
        {
            if (_settings.AlwaysOnTop)
            {
                return;
            }

            for (int index = 0; index < _windows.Count; index++)
            {
                _windows[index].PushToBottom();
            }
        }

        private void HandleAnimationTick(object sender, EventArgs eventArgs)
        {
            double deltaSeconds = _frameClock.Elapsed.TotalSeconds;
            _frameClock.Restart();
            if (deltaSeconds > 0.15)
            {
                deltaSeconds = 1.0 / 30.0;
            }

            for (int index = 0; index < _windows.Count; index++)
            {
                _windows[index].UpdateSnow(deltaSeconds, _settings.Intensity, _paused);
            }
        }

        private void HandleDisplayTick(object sender, EventArgs eventArgs)
        {
            string currentSignature = BuildDisplaySignature();
            if (!String.Equals(currentSignature, _displaySignature, StringComparison.Ordinal))
            {
                RebuildWindows();
            }
        }

        private void RebuildWindows()
        {
            for (int index = 0; index < _windows.Count; index++)
            {
                _windows[index].Close();
            }
            _windows.Clear();

            Forms.Screen[] screens = Forms.Screen.AllScreens;
            int baseSeed = unchecked(Environment.TickCount * 397);
            for (int index = 0; index < screens.Length; index++)
            {
                SnowWindow window = new SnowWindow(screens[index], _settings.AlwaysOnTop, baseSeed + index * 7919);
                _windows.Add(window);
                window.Show();
            }

            _displaySignature = BuildDisplaySignature();
        }

        private static string BuildDisplaySignature()
        {
            StringBuilder signature = new StringBuilder();
            Forms.Screen[] screens = Forms.Screen.AllScreens;
            for (int index = 0; index < screens.Length; index++)
            {
                Forms.Screen screen = screens[index];
                signature.Append(screen.DeviceName).Append('|');
                AppendRectangle(signature, screen.Bounds);
                AppendRectangle(signature, screen.WorkingArea);
                signature.Append(';');
            }
            return signature.ToString();
        }

        private static void AppendRectangle(StringBuilder builder, Rectangle rectangle)
        {
            builder.Append(rectangle.Left).Append(',')
                .Append(rectangle.Top).Append(',')
                .Append(rectangle.Width).Append(',')
                .Append(rectangle.Height).Append('|');
        }

        private void CreateTrayIcon()
        {
            try
            {
                _applicationIcon = Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);
            }
            catch
            {
                _applicationIcon = SystemIcons.Application;
            }

            Forms.ContextMenuStrip menu = new Forms.ContextMenuStrip();
            Forms.ToolStripMenuItem title = new Forms.ToolStripMenuItem("Desktop Snow");
            title.Enabled = false;
            if (_applicationIcon != null)
            {
                title.Image = _applicationIcon.ToBitmap();
            }
            menu.Items.Add(title);
            menu.Items.Add(new Forms.ToolStripSeparator());

            _pauseItem = new Forms.ToolStripMenuItem("Поставить снег на паузу");
            _pauseItem.Click += delegate { TogglePause(); };
            menu.Items.Add(_pauseItem);

            Forms.ToolStripMenuItem intensityMenu = new Forms.ToolStripMenuItem("Интенсивность");
            _lightItem = new Forms.ToolStripMenuItem("Лёгкий снег");
            _normalItem = new Forms.ToolStripMenuItem("Обычный снег");
            _heavyItem = new Forms.ToolStripMenuItem("Сильный снег");
            _lightItem.Click += delegate { SetIntensity(SnowIntensity.Light); };
            _normalItem.Click += delegate { SetIntensity(SnowIntensity.Normal); };
            _heavyItem.Click += delegate { SetIntensity(SnowIntensity.Heavy); };
            intensityMenu.DropDownItems.Add(_lightItem);
            intensityMenu.DropDownItems.Add(_normalItem);
            intensityMenu.DropDownItems.Add(_heavyItem);
            intensityMenu.DropDown.Opened += KeepDropDownAboveSnow;
            menu.Items.Add(intensityMenu);

            Forms.ToolStripMenuItem plowItem = new Forms.ToolStripMenuItem("Вызвать снегоуборщик");
            plowItem.Click += delegate { StartSnowPlows(); };
            menu.Items.Add(plowItem);

            Forms.ToolStripMenuItem clearItem = new Forms.ToolStripMenuItem("Очистить сугробы сразу");
            clearItem.Click += delegate { ClearPiles(); };
            menu.Items.Add(clearItem);

            _topmostItem = new Forms.ToolStripMenuItem("Поверх всех окон");
            _topmostItem.Click += delegate { ToggleAlwaysOnTop(); };
            menu.Items.Add(_topmostItem);

            menu.Items.Add(new Forms.ToolStripSeparator());

            _startupItem = new Forms.ToolStripMenuItem("Запускать вместе с Windows");
            _startupItem.Click += delegate { ToggleStartup(); };
            menu.Items.Add(_startupItem);

            Forms.ToolStripMenuItem aboutItem = new Forms.ToolStripMenuItem("О программе");
            aboutItem.Click += delegate { ShowAbout(); };
            menu.Items.Add(aboutItem);

            menu.Items.Add(new Forms.ToolStripSeparator());
            Forms.ToolStripMenuItem exitItem = new Forms.ToolStripMenuItem("Выход");
            exitItem.Click += delegate { ExitApplication(); };
            menu.Items.Add(exitItem);

            menu.Opening += HandleTrayMenuOpening;
            menu.Opened += KeepDropDownAboveSnow;
            menu.Closing += HandleTrayMenuClosing;

            _notifyIcon = new Forms.NotifyIcon();
            _notifyIcon.Icon = _applicationIcon ?? SystemIcons.Application;
            _notifyIcon.Text = "Desktop Snow — белый снег";
            _notifyIcon.ContextMenuStrip = menu;
            _notifyIcon.Visible = true;
            _notifyIcon.DoubleClick += delegate { TogglePause(); };

            RefreshMenuChecks();
        }

        private void HandleTrayMenuOpening(object sender, System.ComponentModel.CancelEventArgs eventArgs)
        {
            RefreshMenuChecks();
        }

        private static void KeepDropDownAboveSnow(object sender, EventArgs eventArgs)
        {
            Forms.ToolStripDropDown dropDown = sender as Forms.ToolStripDropDown;
            if (dropDown == null || !dropDown.IsHandleCreated)
            {
                return;
            }

            SetWindowPos(
                dropDown.Handle,
                HwndTopmost,
                0,
                0,
                0,
                0,
                SwpNoMove | SwpNoSize | SwpNoActivate);
        }

        private static void HandleTrayMenuClosing(
            object sender,
            Forms.ToolStripDropDownClosingEventArgs eventArgs)
        {
            // Keep the settings panel open after an action so several options
            // can be changed without reopening the tray menu each time.
            // Clicking elsewhere or pressing Escape still closes it normally.
            if (eventArgs.CloseReason == Forms.ToolStripDropDownCloseReason.ItemClicked)
            {
                eventArgs.Cancel = true;
            }
        }

        private void TogglePause()
        {
            _paused = !_paused;
            RefreshMenuChecks();
        }

        private void SetIntensity(SnowIntensity intensity)
        {
            _settings.Intensity = intensity;
            _settings.Save();
            RefreshMenuChecks();
        }

        private void ClearPiles()
        {
            for (int index = 0; index < _windows.Count; index++)
            {
                _windows[index].ClearPile();
            }
        }

        private void StartSnowPlows()
        {
            for (int index = 0; index < _windows.Count; index++)
            {
                _windows[index].StartSnowPlow();
            }
        }

        private void ToggleAlwaysOnTop()
        {
            _settings.AlwaysOnTop = !_settings.AlwaysOnTop;
            for (int index = 0; index < _windows.Count; index++)
            {
                _windows[index].SetAlwaysOnTop(_settings.AlwaysOnTop);
            }
            _settings.Save();
            RefreshMenuChecks();
        }

        private void ToggleStartup()
        {
            bool enable = !AppSettings.IsStartupEnabled();
            try
            {
                AppSettings.SetStartupEnabled(enable);
            }
            catch
            {
                Forms.MessageBox.Show(
                    "Не удалось изменить автозапуск. Проверьте права текущего пользователя.",
                    "Desktop Snow",
                    Forms.MessageBoxButtons.OK,
                    Forms.MessageBoxIcon.Warning);
            }
            RefreshMenuChecks();
        }

        private void RefreshMenuChecks()
        {
            if (_pauseItem == null)
            {
                return;
            }

            _pauseItem.Text = _paused ? "Продолжить снег" : "Поставить снег на паузу";
            _pauseItem.Checked = _paused;
            _lightItem.Checked = _settings.Intensity == SnowIntensity.Light;
            _normalItem.Checked = _settings.Intensity == SnowIntensity.Normal;
            _heavyItem.Checked = _settings.Intensity == SnowIntensity.Heavy;
            _topmostItem.Checked = _settings.AlwaysOnTop;
            _startupItem.Checked = AppSettings.IsStartupEnabled();
        }

        private static void ShowAbout()
        {
            Forms.MessageBox.Show(
                "Desktop Snow 1.1\n\n" +
                "Белый снег поверх рабочего стола с мягким накоплением " +
                "сугроба над панелью задач. При заполнении сугроба " +
                "его автоматически убирает снегоуборочный грузовик.\n\n" +
                "Окно полностью пропускает клики и не мешает работе.",
                "О программе",
                Forms.MessageBoxButtons.OK,
                Forms.MessageBoxIcon.Information);
        }

        private void ExitApplication()
        {
            if (_exiting)
            {
                return;
            }
            _exiting = true;

            if (_animationTimer != null)
            {
                _animationTimer.Stop();
            }
            if (_displayTimer != null)
            {
                _displayTimer.Stop();
            }
            if (_zOrderTimer != null)
            {
                _zOrderTimer.Stop();
            }

            _settings.Save();
            DisposeResources();

            for (int index = 0; index < _windows.Count; index++)
            {
                _windows[index].Close();
            }
            _windows.Clear();

            Shutdown();
        }

        private void DisposeResources()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                if (_notifyIcon.ContextMenuStrip != null)
                {
                    _notifyIcon.ContextMenuStrip.Dispose();
                }
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            if (_applicationIcon != null && !Object.ReferenceEquals(_applicationIcon, SystemIcons.Application))
            {
                _applicationIcon.Dispose();
                _applicationIcon = null;
            }
        }

        private void HandleDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs eventArgs)
        {
            AppLogger.Write("Необработанная ошибка интерфейса.", eventArgs.Exception);
            eventArgs.Handled = true;

            if (!_exiting)
            {
                Forms.MessageBox.Show(
                    "Desktop Snow столкнулся с ошибкой. Подробности записаны в журнал приложения.",
                    "Desktop Snow",
                    Forms.MessageBoxButtons.OK,
                    Forms.MessageBoxIcon.Error);
                ExitApplication();
            }
        }

        private static void HandleUnhandledException(object sender, UnhandledExceptionEventArgs eventArgs)
        {
            AppLogger.Write("Критическая ошибка приложения.", eventArgs.ExceptionObject as Exception);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr window,
            IntPtr insertAfter,
            int x,
            int y,
            int width,
            int height,
            uint flags);
    }
}
